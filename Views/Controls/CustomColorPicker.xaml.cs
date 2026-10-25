using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TextureSwapper.Views.Controls
{
    public partial class CustomColorPicker : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(
                nameof(SelectedColor),
                typeof(Color),
                typeof(CustomColorPicker),
                new FrameworkPropertyMetadata(
                    Colors.Transparent,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedColorChanged
                )
            );

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        private static void OnSelectedColorChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e
        )
        {
            if (d is CustomColorPicker picker && e.NewValue is Color color)
            {
                picker.UpdateLocalPropertiesFromColor(color);
            }
        }

        public SolidColorBrush SelectedColorBrush => new(SelectedColor);

        private bool _isUpdating = false;

        public double A
        {
            get => SelectedColor.A;
            set
            {
                if (SelectedColor.A != (byte)value)
                {
                    UpdateColor((byte)value, R, G, B);
                    OnPropertyChanged();
                }
            }
        }

        public double R
        {
            get => SelectedColor.R;
            set
            {
                if (SelectedColor.R != (byte)value)
                {
                    UpdateColor(A, (byte)value, G, B);
                    OnPropertyChanged();
                }
            }
        }

        public double G
        {
            get => SelectedColor.G;
            set
            {
                if (SelectedColor.G != (byte)value)
                {
                    UpdateColor(A, R, (byte)value, B);
                    OnPropertyChanged();
                }
            }
        }

        public double B
        {
            get => SelectedColor.B;
            set
            {
                if (SelectedColor.B != (byte)value)
                {
                    UpdateColor(A, R, G, (byte)value);
                    OnPropertyChanged();
                }
            }
        }

        public string HexValue
        {
            get =>
                $"#{SelectedColor.A:X2}{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";
            set
            {
                if (_isUpdating)
                {
                    return;
                }

                try
                {
                    Color color = (Color)ColorConverter.ConvertFromString(value);
                    SelectedColor = color;
                }
                catch { }
            }
        }

        private void UpdateColor(double a, double r, double g, double b)
        {
            if (_isUpdating)
            {
                return;
            }

            _isUpdating = true;
            SelectedColor = Color.FromArgb((byte)a, (byte)r, (byte)g, (byte)b);
            UpdateLocalPropertiesFromColor(SelectedColor);
            _isUpdating = false;
        }

        private void UpdateLocalPropertiesFromColor(Color color)
        {
            _isUpdating = true;
            OnPropertyChanged(nameof(SelectedColorBrush));
            OnPropertyChanged(nameof(A));
            OnPropertyChanged(nameof(R));
            OnPropertyChanged(nameof(G));
            OnPropertyChanged(nameof(B));
            OnPropertyChanged(nameof(HexValue));
            _isUpdating = false;
        }

        public CustomColorPicker()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
