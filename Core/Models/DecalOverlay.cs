using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TextureSwapper.Core.Models
{
    public class DecalOverlay : INotifyPropertyChanged
    {
        private string _imagePath = string.Empty;
        public string ImagePath
        {
            get => _imagePath;
            set => SetProperty(ref _imagePath, value);
        }

        private double _x;
        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        private double _y;
        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }

        private double _scale = 1.0;
        public double Scale
        {
            get => _scale;
            set => SetProperty(ref _scale, value);
        }

        private double _rotation;
        public double Rotation
        {
            get => _rotation;
            set => SetProperty(ref _rotation, value);
        }

        private double _opacity = 1.0;
        public double Opacity
        {
            get => _opacity;
            set => SetProperty(ref _opacity, value);
        }

        private double _brightness;
        public double Brightness
        {
            get => _brightness;
            set => SetProperty(ref _brightness, value);
        }

        private double _contrast = 1.0;
        public double Contrast
        {
            get => _contrast;
            set => SetProperty(ref _contrast, value);
        }

        private DecalBlendMode _blendMode = DecalBlendMode.Normal;
        public DecalBlendMode BlendMode
        {
            get => _blendMode;
            set => SetProperty(ref _blendMode, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void SetProperty<T>(
            ref T field,
            T value,
            [CallerMemberName] string? propertyName = null
        )
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public enum DecalBlendMode
    {
        Normal,
        Multiply,
        Screen,
        Overlay,
    }
}
