using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TextureSwapper.Core.Models
{
    public class PropModel : INotifyPropertyChanged
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

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string LibraryBase64Url { get; set; } = string.Empty;
        public string TextureFilename { get; set; } = string.Empty;
        public string? MeshFilename { get; set; }
        public string ThumbnailPath { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        private string? _cachedFullThumbnailPath;
        private bool _thumbnailCacheValid;

        [JsonIgnore]
        public string? FullThumbnailPath
        {
            get
            {
                if (_thumbnailCacheValid)
                {
                    return _cachedFullThumbnailPath;
                }

                _cachedFullThumbnailPath = ResolveFullThumbnailPath();
                _thumbnailCacheValid = true;
                return _cachedFullThumbnailPath;
            }
        }

        private string? ResolveFullThumbnailPath()
        {
            if (string.IsNullOrEmpty(ThumbnailPath))
            {
                return null;
            }

            string fullPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                ThumbnailPath.Replace("\\", "/")
            );
            return File.Exists(fullPath) ? fullPath : null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"{Name} ({Category})";
        }
    }
}
