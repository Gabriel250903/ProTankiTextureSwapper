using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TextureSwapper.Models
{
    public class ShotEffectModel : INotifyPropertyChanged
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

        public string Turret { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string SourceFolder { get; set; } = string.Empty;
        public string PreviewImage { get; set; } = string.Empty;
        public List<string> Targets { get; set; } = [];

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
            return File.Exists(fullPath) ? fullPath : null;
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
