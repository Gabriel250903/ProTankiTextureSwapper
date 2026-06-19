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

        public string VersionText
        {
            get
            {
                if (Name.EndsWith(" LC", StringComparison.OrdinalIgnoreCase) || Name.EndsWith(" Legacy", StringComparison.OrdinalIgnoreCase))
                    return "LC";
                if (Name.EndsWith(" XT", StringComparison.OrdinalIgnoreCase))
                    return "XT";
                return string.Empty;
            }
        }
        public string SourceFolder { get; set; } = string.Empty;
        public string DetailsTarget { get; set; } = string.Empty;
        public string LightmapTarget { get; set; } = string.Empty;
        public string AlphaTarget { get; set; } = string.Empty;
        public string PreviewImage { get; set; } = string.Empty;
        public string? FullPreviewPath
        {
            get
            {
                if (string.IsNullOrEmpty(PreviewImage))
                    return null;
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PreviewImage.Replace("\\", "/"));
                if (File.Exists(fullPath))
                    return fullPath;

                string ext = Path.GetExtension(fullPath).ToLower();
                string[] altExts = [".png", ".jpg", ".jpeg"];
                if (altExts.Contains(ext))
                {
                    foreach (string alt in altExts)
                    {
                        if (alt == ext) continue;
                        string altPath = Path.ChangeExtension(fullPath, alt);
                        if (File.Exists(altPath))
                            return altPath;
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
