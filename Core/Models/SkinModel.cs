using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using TextureSwapper.Helpers.Commands;

namespace TextureSwapper.Core.Models
{
    public partial class SkinModel : INotifyPropertyChanged
    {
        public SkinModel()
        {
            SetModificationCommand = new RelayCommand(param =>
            {
                if (param is string mod && !string.IsNullOrEmpty(mod))
                {
                    bool isAvailable = mod switch
                    {
                        "M0" => IsM0Available,
                        "M1" => IsM1Available,
                        "M2" => IsM2Available,
                        "M3" => IsM3Available,
                        _ => false,
                    };
                    if (isAvailable)
                    {
                        SelectedModification = mod;
                    }
                }
            });
        }

        private bool _isSelected;

        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isPremium;
        public bool IsPremium
        {
            get => _isPremium;
            set
            {
                if (_isPremium != value)
                {
                    _isPremium = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isExperimental;
        public bool IsExperimental
        {
            get =>
                _isExperimental
                || (Category == "Hulls" && (Name.Contains("TX") || SourceFolder.Contains("TX")))
                || Tags.Any(t => t.Equals("Experimental", StringComparison.OrdinalIgnoreCase));
            set
            {
                if (_isExperimental != value)
                {
                    _isExperimental = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ComputedTags));
                    OnPropertyChanged(nameof(WarningMessage));
                }
            }
        }

        private List<string> _tags = [];
        public List<string> Tags
        {
            get => _tags;
            set
            {
                if (_tags != value)
                {
                    _tags = value ?? [];
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ComputedTags));
                    OnPropertyChanged(nameof(PrimaryThemeTag));
                    OnPropertyChanged(nameof(TagsDisplayText));
                    OnPropertyChanged(nameof(IsExperimental));
                }
            }
        }

        [JsonIgnore]
        public IReadOnlyList<string> ComputedTags
        {
            get
            {
                HashSet<string> tags = new(StringComparer.OrdinalIgnoreCase);

                foreach (string tag in Tags)
                {
                    if (!string.IsNullOrWhiteSpace(tag))
                    {
                        tags.Add(tag.Trim());
                    }
                }

                string combined = $"{Name} {SourceFolder} {Category}";

                if (TankiXRegex().IsMatch(combined))
                {
                    tags.Add("Tanki X");
                }

                if (DemonicRegex().IsMatch(combined))
                {
                    tags.Add("Demonic");
                }

                if (LegacyRegex().IsMatch(combined))
                {
                    tags.Add("Legacy");
                }

                if (XtRegex().IsMatch(combined))
                {
                    tags.Add("XT");
                }

                if (PrimeRegex().IsMatch(combined))
                {
                    tags.Add("Prime");
                }

                if (HdRegex().IsMatch(combined))
                {
                    tags.Add("HD");
                }

                if (OldRegex().IsMatch(combined))
                {
                    tags.Add("Old");
                }

                if (IcRegex().IsMatch(combined))
                {
                    tags.Add("IC");
                }

                if (SpRegex().IsMatch(combined))
                {
                    tags.Add("SP");
                }

                if (VtRegex().IsMatch(combined))
                {
                    tags.Add("VT");
                }

                if (RtRegex().IsMatch(combined))
                {
                    tags.Add("RT");
                }

                if (CsRegex().IsMatch(combined))
                {
                    tags.Add("CS");
                }

                if (IsExperimental)
                {
                    tags.Add("Experimental");
                }

                if (tags.Count == 0 && (Category is "Turrets" or "Hulls"))
                {
                    if (!string.IsNullOrWhiteSpace(ItemName) && Name.StartsWith(ItemName, StringComparison.OrdinalIgnoreCase))
                    {
                        string remainder = Name[ItemName.Length..].Trim();
                        string cleanRemainder = OnXtSuffixRegex().Replace(remainder, string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(cleanRemainder))
                        {
                            tags.Add(cleanRemainder);
                        }
                    }
                    if (tags.Count == 0)
                    {
                        tags.Add("Standard");
                    }
                }

                return [.. tags];
            }
        }

        [JsonIgnore]
        public string? PrimaryThemeTag
        {
            get
            {
                IReadOnlyList<string> all = ComputedTags;
                if (all.Contains("Demonic", StringComparer.OrdinalIgnoreCase)) return "Demonic";
                if (all.Contains("Tanki X", StringComparer.OrdinalIgnoreCase)) return "Tanki X";
                if (all.Contains("HD", StringComparer.OrdinalIgnoreCase)) return "HD";
                if (all.Contains("Prime", StringComparer.OrdinalIgnoreCase)) return "Prime";
                if (all.Contains("XT", StringComparer.OrdinalIgnoreCase)) return "XT";
                if (all.Contains("Legacy", StringComparer.OrdinalIgnoreCase)) return "Legacy";
                if (all.Contains("IC", StringComparer.OrdinalIgnoreCase)) return "IC";
                if (all.Contains("SP", StringComparer.OrdinalIgnoreCase)) return "SP";
                if (all.Contains("VT", StringComparer.OrdinalIgnoreCase)) return "VT";
                if (all.Contains("RT", StringComparer.OrdinalIgnoreCase)) return "RT";
                if (all.Contains("CS", StringComparer.OrdinalIgnoreCase)) return "CS";
                if (all.Contains("Old", StringComparer.OrdinalIgnoreCase)) return "Old";
                string? firstNonExp = all.FirstOrDefault(t => !t.Equals("Experimental", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(firstNonExp)) return firstNonExp;
                if (all.Count > 0) return all[0];
                return (Category is "Turrets" or "Hulls") ? "Standard" : null;
            }
        }

        [JsonIgnore]
        public string TagsDisplayText => string.Join(", ", ComputedTags);

        private string _warningMessage = string.Empty;
        public string WarningMessage
        {
            get =>
                !string.IsNullOrEmpty(_warningMessage) ? _warningMessage
                : IsExperimental
                    ? "Complex high-polygon model: Initial loading may take slightly longer and occasional FPS stutters may occur."
                : string.Empty;
            set
            {
                if (_warningMessage != value)
                {
                    _warningMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _category = string.Empty;
        public string Category
        {
            get => _category;
            set => _category = value ?? string.Empty;
        }

        private string _subCategory = string.Empty;
        public string SubCategory
        {
            get => _subCategory;
            set
            {
                _subCategory = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        private string _itemName = string.Empty;
        public string ItemName
        {
            get => _itemName;
            set
            {
                string newVal = value ?? string.Empty;
                if (_itemName != newVal)
                {
                    _itemName = newVal;
                    OnPropertyChanged();
                }
            }
        }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => _name = value ?? string.Empty;
        }

        [JsonIgnore]
        public string VersionText =>
            Name.EndsWith(" LC", StringComparison.OrdinalIgnoreCase)
            || Name.EndsWith(" Legacy", StringComparison.OrdinalIgnoreCase)
                ? "LC"
            : Name.EndsWith(" XT", StringComparison.OrdinalIgnoreCase) ? "XT"
            : string.Empty;

        [JsonIgnore]
        public string VersionDisplay =>
            HasModifications && !string.IsNullOrEmpty(SelectedModification)
                ? (
                    !string.IsNullOrEmpty(VersionText)
                        ? $"{VersionText} • {SelectedModification}"
                        : SelectedModification
                )
                : VersionText;

        [JsonIgnore]
        public ICommand SetModificationCommand { get; }

        private string _selectedModification = "M3";

        [JsonIgnore]
        public string SelectedModification
        {
            get => _selectedModification;
            set
            {
                string newVal = value ?? "M3";
                if (_selectedModification != newVal)
                {
                    _selectedModification = newVal;
                    _previewCacheValid = false;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EffectiveSourceFolder));
                    OnPropertyChanged(nameof(VersionDisplay));
                    OnPropertyChanged(nameof(FullPreviewPath));
                    OnPropertyChanged(nameof(IsM0Selected));
                    OnPropertyChanged(nameof(IsM1Selected));
                    OnPropertyChanged(nameof(IsM2Selected));
                    OnPropertyChanged(nameof(IsM3Selected));
                }
            }
        }

        private bool _hasModifications;

        [JsonIgnore]
        public bool HasModifications
        {
            get => _hasModifications;
            private set
            {
                if (_hasModifications != value)
                {
                    _hasModifications = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(VersionDisplay));
                }
            }
        }

        private bool _isM0Available;

        [JsonIgnore]
        public bool IsM0Available
        {
            get => _isM0Available;
            private set
            {
                if (_isM0Available != value)
                {
                    _isM0Available = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(M0ToolTip));
                }
            }
        }

        private bool _isM1Available;

        [JsonIgnore]
        public bool IsM1Available
        {
            get => _isM1Available;
            private set
            {
                if (_isM1Available != value)
                {
                    _isM1Available = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(M1ToolTip));
                }
            }
        }

        private bool _isM2Available;

        [JsonIgnore]
        public bool IsM2Available
        {
            get => _isM2Available;
            private set
            {
                if (_isM2Available != value)
                {
                    _isM2Available = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(M2ToolTip));
                }
            }
        }

        private bool _isM3Available;

        [JsonIgnore]
        public bool IsM3Available
        {
            get => _isM3Available;
            private set
            {
                if (_isM3Available != value)
                {
                    _isM3Available = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(M3ToolTip));
                }
            }
        }

        [JsonIgnore]
        public bool IsM0Selected => SelectedModification == "M0";

        [JsonIgnore]
        public bool IsM1Selected => SelectedModification == "M1";

        [JsonIgnore]
        public bool IsM2Selected => SelectedModification == "M2";

        [JsonIgnore]
        public bool IsM3Selected => SelectedModification == "M3";

        [JsonIgnore]
        public string M0ToolTip =>
            IsM0Available ? "Select M0 texture" : "M0 texture folder not found on disk";

        [JsonIgnore]
        public string M1ToolTip =>
            IsM1Available ? "Select M1 texture" : "M1 texture folder not found on disk";

        [JsonIgnore]
        public string M2ToolTip =>
            IsM2Available ? "Select M2 texture" : "M2 texture folder not found on disk";

        [JsonIgnore]
        public string M3ToolTip =>
            IsM3Available ? "Select M3 texture" : "M3 texture folder not found on disk";

        [JsonIgnore]
        public ObservableCollection<string> AvailableModifications { get; } = [];

        private string _sourceFolder = string.Empty;
        public string SourceFolder
        {
            get => _sourceFolder;
            set
            {
                _sourceFolder = value ?? string.Empty;
                OnPropertyChanged(nameof(EffectiveSourceFolder));
            }
        }

        [JsonIgnore]
        public string EffectiveSourceFolder =>
            string.IsNullOrEmpty(SourceFolder) ? string.Empty
            : HasModifications && !string.IsNullOrEmpty(SelectedModification)
                ? GetModificationPath(SourceFolder, SelectedModification)
            : SourceFolder;

        private bool _modificationsCached = false;

        public void RefreshAvailableModifications(bool force = false)
        {
            if (_modificationsCached && !force)
            {
                return;
            }

            ComputeModificationsInternal();
            _modificationsCached = true;
        }

        private void ComputeModificationsInternal()
        {
            if (Category is not "Hulls" and not "Turrets")
            {
                AvailableModifications.Clear();
                HasModifications = false;
                IsM0Available = false;
                IsM1Available = false;
                IsM2Available = false;
                IsM3Available = false;
                return;
            }

            string normalized = SourceFolder.Replace('\\', '/');
            Match match = ModificationFolderRegex().Match(normalized);
            if (!match.Success)
            {
                AvailableModifications.Clear();
                HasModifications = false;
                IsM0Available = false;
                IsM1Available = false;
                IsM2Available = false;
                IsM3Available = false;
                return;
            }

            string parentRelFolder = match.Groups[1].Value;
            string asmDir =
                Path.GetDirectoryName(typeof(SkinModel).Assembly.Location)
                ?? AppContext.BaseDirectory;
            string parentFullFolder = Path.Combine(asmDir, parentRelFolder);
            string devDir = Path.GetFullPath(
                Path.Combine(asmDir, "..", "..", "..", parentRelFolder)
            );

            AvailableModifications.Clear();

            string[] mods = ["M0", "M1", "M2", "M3"];
            foreach (string mod in mods)
            {
                string modFolder = Path.Combine(parentFullFolder, mod);
                string devModFolder = Path.Combine(devDir, mod);

                bool isAvail =
                    (Directory.Exists(modFolder) && HasTextureFiles(modFolder))
                    || (Directory.Exists(devModFolder) && HasTextureFiles(devModFolder));

                switch (mod)
                {
                    case "M0":
                        IsM0Available = isAvail;
                        break;
                    case "M1":
                        IsM1Available = isAvail;
                        break;
                    case "M2":
                        IsM2Available = isAvail;
                        break;
                    case "M3":
                        IsM3Available = isAvail;
                        break;
                }

                if (isAvail)
                {
                    AvailableModifications.Add(mod);
                }
            }

            HasModifications = AvailableModifications.Count > 0;

            if (AvailableModifications.Count > 0)
            {
                if (!AvailableModifications.Contains(SelectedModification))
                {
                    SelectedModification = AvailableModifications.Contains("M3")
                        ? "M3"
                        : AvailableModifications.Last();
                }
            }
        }

        [JsonIgnore]
        public string DetailsTarget { get; set; } = string.Empty;

        [JsonPropertyName("DetailsTarget")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DetailsTargetForSerialization
        {
            get => string.IsNullOrEmpty(DetailsTarget) ? null : DetailsTarget;
            set => DetailsTarget = value ?? string.Empty;
        }

        [JsonIgnore]
        public string LightmapTarget { get; set; } = string.Empty;

        [JsonPropertyName("LightmapTarget")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LightmapTargetForSerialization
        {
            get => string.IsNullOrEmpty(LightmapTarget) ? null : LightmapTarget;
            set => LightmapTarget = value ?? string.Empty;
        }

        [JsonIgnore]
        public string AlphaTarget { get; set; } = string.Empty;

        [JsonPropertyName("AlphaTarget")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AlphaTargetForSerialization
        {
            get => string.IsNullOrEmpty(AlphaTarget) ? null : AlphaTarget;
            set => AlphaTarget = value ?? string.Empty;
        }

        [JsonIgnore]
        public string ModelTarget { get; set; } = string.Empty;

        [JsonPropertyName("ModelTarget")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ModelTargetForSerialization
        {
            get => string.IsNullOrEmpty(ModelTarget) ? null : ModelTarget;
            set => ModelTarget = value ?? string.Empty;
        }

        [JsonIgnore]
        public string TracksTarget { get; set; } = string.Empty;

        [JsonPropertyName("TracksTarget")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TracksTargetForSerialization
        {
            get => string.IsNullOrEmpty(TracksTarget) ? null : TracksTarget;
            set => TracksTarget = value ?? string.Empty;
        }

        [JsonIgnore]
        public string ImagesXmlTarget { get; set; } = string.Empty;

        [JsonPropertyName("ImagesXmlTarget")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ImagesXmlTargetForSerialization
        {
            get => string.IsNullOrEmpty(ImagesXmlTarget) ? null : ImagesXmlTarget;
            set => ImagesXmlTarget = value ?? string.Empty;
        }

        private string _previewImage = string.Empty;
        public string PreviewImage
        {
            get => _previewImage;
            set => _previewImage = value ?? string.Empty;
        }

        private string? _cachedFullPreviewPath;
        private bool _previewCacheValid;

        [JsonIgnore]
        public string? FullPreviewPath
        {
            get
            {
                if (_previewCacheValid)
                {
                    return _cachedFullPreviewPath;
                }

                _cachedFullPreviewPath = ResolveFullPreviewPath();
                _previewCacheValid = true;
                return _cachedFullPreviewPath;
            }
        }

        public const string FallbackPreviewUri = "pack://application:,,,/Assets/mystery_hmm.png";

        private string? ResolveFullPreviewPath()
        {
            if (string.IsNullOrEmpty(PreviewImage))
            {
                return FallbackPreviewUri;
            }

            if (PreviewImage.StartsWith("pack:", StringComparison.OrdinalIgnoreCase))
            {
                return PreviewImage;
            }
            string asmDir =
                Path.GetDirectoryName(typeof(SkinModel).Assembly.Location)
                ?? AppContext.BaseDirectory;

            if (!string.IsNullOrEmpty(PreviewImage))
            {
                if (HasModifications && !string.IsNullOrEmpty(SelectedModification))
                {
                    string modPath = GetModificationPath(PreviewImage, SelectedModification);
                    if (!string.Equals(modPath, PreviewImage, StringComparison.OrdinalIgnoreCase))
                    {
                        string modFullPath = Path.Combine(asmDir, modPath.Replace("\\", "/"));
                        string? matchedMod = CheckFileWithExtensions(modFullPath);
                        if (matchedMod != null)
                        {
                            return matchedMod;
                        }

                        string devModPath = Path.GetFullPath(
                            Path.Combine(asmDir, "..", "..", "..", modPath.Replace("\\", "/"))
                        );
                        matchedMod = CheckFileWithExtensions(devModPath);
                        if (matchedMod != null)
                        {
                            return matchedMod;
                        }
                    }

                    string dir = Path.GetDirectoryName(PreviewImage) ?? string.Empty;
                    string fnWithoutExt = Path.GetFileNameWithoutExtension(PreviewImage);
                    string ext = Path.GetExtension(PreviewImage);
                    string suffixedRelative = Path.Combine(
                            dir,
                            $"{fnWithoutExt}_{SelectedModification}{ext}"
                        )
                        .Replace("\\", "/");

                    string suffixedFullPath = Path.Combine(asmDir, suffixedRelative);
                    string? matchedSuffix = CheckFileWithExtensions(suffixedFullPath);
                    if (matchedSuffix != null)
                    {
                        return matchedSuffix;
                    }

                    string devSuffixPath = Path.GetFullPath(
                        Path.Combine(asmDir, "..", "..", "..", suffixedRelative)
                    );
                    matchedSuffix = CheckFileWithExtensions(devSuffixPath);
                    if (matchedSuffix != null)
                    {
                        return matchedSuffix;
                    }
                }

                string fullPath = Path.Combine(asmDir, PreviewImage.Replace("\\", "/"));
                string? matchedBase = CheckFileWithExtensions(fullPath);
                if (matchedBase != null)
                {
                    return matchedBase;
                }

                string devBasePath = Path.GetFullPath(
                    Path.Combine(asmDir, "..", "..", "..", PreviewImage.Replace("\\", "/"))
                );
                string? matchedDev = CheckFileWithExtensions(devBasePath);
                if (matchedDev != null)
                {
                    return matchedDev;
                }
            }

            return FallbackPreviewUri;
        }

        private static string GetModificationPath(string path, string mod)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', '/');
            return ModificationReplaceRegex().Replace(normalized, $"/{mod}");
        }

        [GeneratedRegex(@"\b(TX|Tanki X)\b|TX_", RegexOptions.IgnoreCase)]
        private static partial Regex TankiXRegex();

        [GeneratedRegex(@"\b(Demonic|DC)\b", RegexOptions.IgnoreCase)]
        private static partial Regex DemonicRegex();

        [GeneratedRegex(@"\b(Legacy|LC)\b", RegexOptions.IgnoreCase)]
        private static partial Regex LegacyRegex();

        [GeneratedRegex(@"\bXT\b", RegexOptions.IgnoreCase)]
        private static partial Regex XtRegex();

        [GeneratedRegex(@"\bPrime\b", RegexOptions.IgnoreCase)]
        private static partial Regex PrimeRegex();

        [GeneratedRegex(@"\bHD\b", RegexOptions.IgnoreCase)]
        private static partial Regex HdRegex();

        [GeneratedRegex(@"\bOld\b", RegexOptions.IgnoreCase)]
        private static partial Regex OldRegex();

        [GeneratedRegex(@"\bIC\b", RegexOptions.IgnoreCase)]
        private static partial Regex IcRegex();

        [GeneratedRegex(@"\bSP\b", RegexOptions.IgnoreCase)]
        private static partial Regex SpRegex();

        [GeneratedRegex(@"\bVT\b", RegexOptions.IgnoreCase)]
        private static partial Regex VtRegex();

        [GeneratedRegex(@"\bRT\b", RegexOptions.IgnoreCase)]
        private static partial Regex RtRegex();

        [GeneratedRegex(@"\bCS\b", RegexOptions.IgnoreCase)]
        private static partial Regex CsRegex();

        [GeneratedRegex(@"^(.*)/M[0-3]$", RegexOptions.IgnoreCase)]
        private static partial Regex ModificationFolderRegex();

        [GeneratedRegex(@"/M[0-3](?=/|$)", RegexOptions.IgnoreCase)]
        private static partial Regex ModificationReplaceRegex();

        [GeneratedRegex(@"\s+on\s+XT$", RegexOptions.IgnoreCase)]
        private static partial Regex OnXtSuffixRegex();

        private static string? CheckFileWithExtensions(string path)
        {
            if (File.Exists(path))
            {
                return path;
            }

            string ext = Path.GetExtension(path).ToLower();
            string[] altExts = [".png", ".jpg", ".jpeg"];
            if (altExts.Contains(ext))
            {
                foreach (string alt in altExts)
                {
                    if (alt == ext)
                    {
                        continue;
                    }

                    string altPath = Path.ChangeExtension(path, alt);
                    if (File.Exists(altPath))
                    {
                        return altPath;
                    }
                }
            }
            return null;
        }

        private static bool HasTextureFiles(string folder)
        {
            try
            {
                return Directory
                    .EnumerateFiles(folder)
                    .Any(f =>
                    {
                        string fn = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                        return fn is "details" or "lightmap" or "alpha";
                    });
            }
            catch
            {
                return false;
            }
        }

        public void NotifyPreviewChanged()
        {
            _previewCacheValid = false;
            OnPropertyChanged(nameof(FullPreviewPath));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
