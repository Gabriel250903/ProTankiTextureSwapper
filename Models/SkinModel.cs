using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TextureSwapper.Models
{
    public class SkinModel : INotifyPropertyChanged
    {
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

        private string _category = string.Empty;
        public string Category
        {
            get => _category;
            set => _category = value ?? string.Empty;
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
        public string VersionText => Name.EndsWith(" LC", StringComparison.OrdinalIgnoreCase) || Name.EndsWith(" Legacy", StringComparison.OrdinalIgnoreCase)
                    ? "LC"
                    : Name.EndsWith(" XT", StringComparison.OrdinalIgnoreCase) ? "XT" : string.Empty;
        private string _sourceFolder = string.Empty;
        public string SourceFolder
        {
            get => _sourceFolder;
            set => _sourceFolder = value ?? string.Empty;
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

        private string? ResolveFullPreviewPath()
        {
            if (string.IsNullOrEmpty(PreviewImage))
            {
                return null;
            }

            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PreviewImage.Replace("\\", "/"));
            if (File.Exists(fullPath))
            {
                return fullPath;
            }

            string ext = Path.GetExtension(fullPath).ToLower();
            string[] altExts = [".png", ".jpg", ".jpeg"];
            if (altExts.Contains(ext))
            {
                foreach (string alt in altExts)
                {
                    if (alt == ext)
                    {
                        continue;
                    }

                    string altPath = Path.ChangeExtension(fullPath, alt);
                    if (File.Exists(altPath))
                    {
                        return altPath;
                    }
                }
            }
            return null;
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
