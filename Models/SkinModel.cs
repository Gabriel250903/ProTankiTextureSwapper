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

        public string Category { get; set; } = string.Empty;
        private string _itemName = string.Empty;
        public string ItemName
        {
            get => _itemName;
            set
            {
                if (_itemName != value)
                {
                    _itemName = value;
                    OnPropertyChanged();
                }
            }
        }
        public string Name { get; set; } = string.Empty;

        [JsonIgnore]
        public string VersionText => Name.EndsWith(" LC", StringComparison.OrdinalIgnoreCase) || Name.EndsWith(" Legacy", StringComparison.OrdinalIgnoreCase)
                    ? "LC"
                    : Name.EndsWith(" XT", StringComparison.OrdinalIgnoreCase) ? "XT" : string.Empty;
        public string SourceFolder { get; set; } = string.Empty;

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

        public string PreviewImage { get; set; } = string.Empty;

        [JsonIgnore]
        public string? FullPreviewPath
        {
            get
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
        }

        public void NotifyPreviewChanged()
        {
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
