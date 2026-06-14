using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace TextureSwapper.Models
{
    public class SkinModel : INotifyPropertyChanged
    {
        private bool _isSelected;

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
        public string ItemName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string SourceFolder { get; set; } = string.Empty;
        public string DetailsTarget { get; set; } = string.Empty;
        public string LightmapTarget { get; set; } = string.Empty;
        public string AlphaTarget { get; set; } = string.Empty;
        public string PreviewImage { get; set; } = string.Empty;
        public string FullPreviewPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PreviewImage);

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
