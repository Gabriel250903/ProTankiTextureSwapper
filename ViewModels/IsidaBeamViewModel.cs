using Microsoft.Win32;
using Serilog;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TextureSwapper.Helpers;
using TextureSwapper.Models;

namespace TextureSwapper.ViewModels
{
    public class IsidaBeamViewModel : ViewModelBase
    {
        private readonly Action _onSettingsChanged;
        private static readonly Random _random = new();

        private double _hue;
        private double _saturation = 100.0;
        private double _brightness = 0.0;
        private double _contrast = 1.0;
        private bool _invert = false;
        private string _blendMode = "Multiply";
        private double _overlayOpacity = 100.0;
        private string _texturePath = string.Empty;
        private double _redScale = 100.0;
        private double _greenScale = 100.0;
        private double _blueScale = 100.0;
        private double _pixelSize = 1.0;
        private double _noise = 0.0;
        private double _blurRadius = 0.0;

        private double _textureScaleX = 1.0;
        private double _textureScaleY = 1.0;
        private double _textureOffsetX = 0.0;
        private double _textureOffsetY = 0.0;
        private double _waveAmplitude = 0.0;
        private double _waveFrequency = 1.0;
        private double _beamThickness = 1.0;
        private double _coreSharpness = 1.0;
        private double _innerCoreBoost = 0.0;

        private BitmapSource? _preview;

        public string Title { get; }

        public double Hue
        {
            get => _hue;
            set
            {
                if (SetProperty(ref _hue, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double Saturation
        {
            get => _saturation;
            set
            {
                if (SetProperty(ref _saturation, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double Brightness
        {
            get => _brightness;
            set
            {
                if (SetProperty(ref _brightness, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double Contrast
        {
            get => _contrast;
            set
            {
                if (SetProperty(ref _contrast, value))
                {
                    NotifyChanged();
                }
            }
        }

        public bool Invert
        {
            get => _invert;
            set
            {
                if (SetProperty(ref _invert, value))
                {
                    NotifyChanged();
                }
            }
        }

        public string BlendMode
        {
            get => _blendMode;
            set
            {
                if (SetProperty(ref _blendMode, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double OverlayOpacity
        {
            get => _overlayOpacity;
            set
            {
                if (SetProperty(ref _overlayOpacity, value))
                {
                    NotifyChanged();
                }
            }
        }

        public string TexturePath
        {
            get => _texturePath;
            set
            {
                if (SetProperty(ref _texturePath, value))
                {
                    OnPropertyChanged(nameof(TextureName));
                    LoadCustomTextureFile(value);
                }
            }
        }

        public string TextureName => string.IsNullOrEmpty(TexturePath) ? "None (Color Tint Only)" : Path.GetFileName(TexturePath);

        public CustomTextureInfo? Texture { get; private set; }

        public double RedScale
        {
            get => _redScale;
            set
            {
                if (SetProperty(ref _redScale, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double GreenScale
        {
            get => _greenScale;
            set
            {
                if (SetProperty(ref _greenScale, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double BlueScale
        {
            get => _blueScale;
            set
            {
                if (SetProperty(ref _blueScale, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double PixelSize
        {
            get => _pixelSize;
            set
            {
                if (SetProperty(ref _pixelSize, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double Noise
        {
            get => _noise;
            set
            {
                if (SetProperty(ref _noise, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double BlurRadius
        {
            get => _blurRadius;
            set
            {
                if (SetProperty(ref _blurRadius, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double TextureScaleX
        {
            get => _textureScaleX;
            set
            {
                if (SetProperty(ref _textureScaleX, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double TextureScaleY
        {
            get => _textureScaleY;
            set
            {
                if (SetProperty(ref _textureScaleY, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double TextureOffsetX
        {
            get => _textureOffsetX;
            set
            {
                if (SetProperty(ref _textureOffsetX, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double TextureOffsetY
        {
            get => _textureOffsetY;
            set
            {
                if (SetProperty(ref _textureOffsetY, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double WaveAmplitude
        {
            get => _waveAmplitude;
            set
            {
                if (SetProperty(ref _waveAmplitude, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double WaveFrequency
        {
            get => _waveFrequency;
            set
            {
                if (SetProperty(ref _waveFrequency, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double BeamThickness
        {
            get => _beamThickness;
            set
            {
                if (SetProperty(ref _beamThickness, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double CoreSharpness
        {
            get => _coreSharpness;
            set
            {
                if (SetProperty(ref _coreSharpness, value))
                {
                    NotifyChanged();
                }
            }
        }

        public double InnerCoreBoost
        {
            get => _innerCoreBoost;
            set
            {
                if (SetProperty(ref _innerCoreBoost, value))
                {
                    NotifyChanged();
                }
            }
        }

        public BitmapSource? Preview
        {
            get => _preview;
            set => SetProperty(ref _preview, value);
        }

        public ICommand LoadTextureCommand { get; }
        public ICommand ClearTextureCommand { get; }

        public IsidaBeamViewModel(string title, double defaultHue, Action onSettingsChanged)
        {
            Title = title;
            _hue = defaultHue;
            _onSettingsChanged = onSettingsChanged;

            LoadTextureCommand = new RelayCommand(_ => ExecuteLoadTexture());
            ClearTextureCommand = new RelayCommand(_ => TexturePath = string.Empty);
        }

        private void NotifyChanged()
        {
            _onSettingsChanged?.Invoke();
        }

        private void ExecuteLoadTexture()
        {
            OpenFileDialog dialog = new()
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                Title = $"Select Custom Overlay Texture for {Title}"
            };

            if (dialog.ShowDialog() == true)
            {
                TexturePath = dialog.FileName;
            }
        }

        private void LoadCustomTextureFile(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    Texture = null;
                    NotifyChanged();
                    return;
                }

                Log.Information($"Loading custom overlay texture from {path}");
                Uri uri = new(path);
                BitmapImage bitmap = new();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                FormatConvertedBitmap formattedBitmap = new(bitmap, PixelFormats.Bgr32, null, 0);
                int w = formattedBitmap.PixelWidth;
                int h = formattedBitmap.PixelHeight;
                int stride = w * 4;
                byte[] px = new byte[h * stride];
                formattedBitmap.CopyPixels(px, stride, 0);

                Texture = new CustomTextureInfo
                {
                    Pixels = px,
                    Width = w,
                    Height = h,
                    Stride = stride
                };

                NotifyChanged();
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to load custom texture from {path}");
                Texture = null;
                NotifyChanged();
            }
        }

        public BitmapSource Render(byte[]? templatePixels, int templateWidth, int templateHeight)
        {
            if (templatePixels == null)
            {
                return null!;
            }

            int width = templateWidth;
            int height = templateHeight;
            int stride = width * 4;
            byte[] outputPixels = new byte[height * stride];

            int pixelationSize = (int)Math.Max(1, Math.Round(PixelSize));
            int blurRadius = (int)BlurRadius;
            double thickness = BeamThickness;
            double sharpness = CoreSharpness;
            double coreBoost = InnerCoreBoost;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int pixelOffset = ((y * width) + x) * 4;

                    if (x is < 31 or > 96)
                    {
                        outputPixels[pixelOffset] = 255;
                        outputPixels[pixelOffset + 1] = 255;
                        outputPixels[pixelOffset + 2] = 255;
                        outputPixels[pixelOffset + 3] = 255;
                        continue;
                    }

                    int sampleX = x;
                    int sampleY = y;
                    if (WaveAmplitude > 0)
                    {
                        double shift = Math.Sin(y * WaveFrequency * 0.05) * WaveAmplitude;
                        sampleX = (int)Math.Round(x + shift);
                        sampleX = Math.Clamp(sampleX, 31, 96);
                    }

                    if (thickness != 1.0)
                    {
                        double center = 63.5;
                        double relX = sampleX - center;
                        relX /= thickness;
                        sampleX = (int)Math.Round(center + relX);
                    }

                    if (pixelationSize > 1)
                    {
                        sampleX = 31 + ((sampleX - 31) / pixelationSize * pixelationSize);
                        sampleY = y / pixelationSize * pixelationSize;
                        sampleX = Math.Clamp(sampleX, 31, 96);
                        sampleY = Math.Clamp(sampleY, 0, height - 1);
                    }

                    byte templatePixelValue = 0;
                    if (sampleX >= 31 && sampleX <= 96 && sampleY >= 0 && sampleY < height)
                    {
                        int sampleIdx = ((sampleY * width) + sampleX) * 4;
                        templatePixelValue = templatePixels[sampleIdx + 2];
                    }

                    double luminance = templatePixelValue / 255.0;
                    if (sharpness != 1.0 && luminance > 0)
                    {
                        luminance = Math.Pow(luminance, 1.0 / sharpness);
                    }

                    ColorHelper.HslToRgb(Hue / 360.0, Saturation / 100.0, luminance, out byte rgbRed, out byte rgbGreen, out byte rgbBlue);

                    double redChannel = rgbRed / 255.0 * (RedScale / 100.0);
                    double greenChannel = rgbGreen / 255.0 * (GreenScale / 100.0);
                    double blueChannel = rgbBlue / 255.0 * (BlueScale / 100.0);

                    double brightnessOffset = Brightness / 100.0;
                    redChannel += brightnessOffset;
                    greenChannel += brightnessOffset;
                    blueChannel += brightnessOffset;

                    redChannel = ((redChannel - 0.5) * Contrast) + 0.5;
                    greenChannel = ((greenChannel - 0.5) * Contrast) + 0.5;
                    blueChannel = ((blueChannel - 0.5) * Contrast) + 0.5;

                    redChannel = Math.Clamp(redChannel, 0.0, 1.0);
                    greenChannel = Math.Clamp(greenChannel, 0.0, 1.0);
                    blueChannel = Math.Clamp(blueChannel, 0.0, 1.0);

                    if (Invert)
                    {
                        redChannel = 1.0 - redChannel;
                        greenChannel = 1.0 - greenChannel;
                        blueChannel = 1.0 - blueChannel;
                    }

                    if (Noise > 0)
                    {
                        double noiseVal = (_random.NextDouble() - 0.5) * (Noise / 100.0);
                        redChannel = Math.Clamp(redChannel + noiseVal, 0.0, 1.0);
                        greenChannel = Math.Clamp(greenChannel + noiseVal, 0.0, 1.0);
                        blueChannel = Math.Clamp(blueChannel + noiseVal, 0.0, 1.0);
                    }

                    if (Texture != null)
                    {
                        double normalizedTextureX = (sampleX - 31) / 65.0;
                        double normalizedTextureY = sampleY / 511.0;

                        normalizedTextureX = ((normalizedTextureX * TextureScaleX) + TextureOffsetX) % 1.0;
                        normalizedTextureY = ((normalizedTextureY * TextureScaleY) + TextureOffsetY) % 1.0;

                        if (normalizedTextureX < 0)
                        {
                            normalizedTextureX += 1.0;
                        }

                        if (normalizedTextureY < 0)
                        {
                            normalizedTextureY += 1.0;
                        }

                        int texturePixelX = (int)(normalizedTextureX * (Texture.Width - 1));
                        int texturePixelY = (int)(normalizedTextureY * (Texture.Height - 1));

                        texturePixelX = Math.Clamp(texturePixelX, 0, Texture.Width - 1);
                        texturePixelY = Math.Clamp(texturePixelY, 0, Texture.Height - 1);

                        int texturePixelOffset = ((texturePixelY * Texture.Width) + texturePixelX) * 4;
                        double overlayRed = Texture.Pixels[texturePixelOffset + 2] / 255.0;
                        double overlayGreen = Texture.Pixels[texturePixelOffset + 1] / 255.0;
                        double overlayBlue = Texture.Pixels[texturePixelOffset] / 255.0;

                        double blendedR = redChannel;
                        double blendedG = greenChannel;
                        double blendedB = blueChannel;

                        switch (BlendMode)
                        {
                            case "Normal":
                                blendedR = overlayRed;
                                blendedG = overlayGreen;
                                blendedB = overlayBlue;
                                break;
                            case "Multiply":
                                blendedR = redChannel * overlayRed;
                                blendedG = greenChannel * overlayGreen;
                                blendedB = blueChannel * overlayBlue;
                                break;
                            case "Screen":
                                blendedR = 1.0 - ((1.0 - redChannel) * (1.0 - overlayRed));
                                blendedG = 1.0 - ((1.0 - greenChannel) * (1.0 - overlayGreen));
                                blendedB = 1.0 - ((1.0 - blueChannel) * (1.0 - overlayBlue));
                                break;
                            case "Overlay":
                                blendedR = redChannel < 0.5 ? 2.0 * redChannel * overlayRed : 1.0 - (2.0 * (1.0 - redChannel) * (1.0 - overlayRed));
                                blendedG = greenChannel < 0.5 ? 2.0 * greenChannel * overlayGreen : 1.0 - (2.0 * (1.0 - greenChannel) * (1.0 - overlayGreen));
                                blendedB = blueChannel < 0.5 ? 2.0 * blueChannel * overlayBlue : 1.0 - (2.0 * (1.0 - blueChannel) * (1.0 - overlayBlue));
                                break;
                            case "Additive":
                                blendedR = Math.Min(redChannel + overlayRed, 1.0);
                                blendedG = Math.Min(greenChannel + overlayGreen, 1.0);
                                blendedB = Math.Min(blueChannel + overlayBlue, 1.0);
                                break;
                        }

                        double opacity = OverlayOpacity / 100.0;
                        redChannel += (blendedR - redChannel) * opacity;
                        greenChannel += (blendedG - greenChannel) * opacity;
                        blueChannel += (blendedB - blueChannel) * opacity;
                    }

                    if (coreBoost > 0)
                    {
                        double coreThreshold = 0.65;
                        if (luminance > coreThreshold)
                        {
                            double factor = (luminance - coreThreshold) / (1.0 - coreThreshold) * (coreBoost / 100.0);
                            redChannel = Math.Clamp(redChannel + ((1.0 - redChannel) * factor), 0.0, 1.0);
                            greenChannel = Math.Clamp(greenChannel + ((1.0 - greenChannel) * factor), 0.0, 1.0);
                            blueChannel = Math.Clamp(blueChannel + ((1.0 - blueChannel) * factor), 0.0, 1.0);
                        }
                    }

                    outputPixels[pixelOffset] = (byte)(blueChannel * 255);
                    outputPixels[pixelOffset + 1] = (byte)(greenChannel * 255);
                    outputPixels[pixelOffset + 2] = (byte)(redChannel * 255);
                    outputPixels[pixelOffset + 3] = 255;
                }
            }

            if (blurRadius > 0)
            {
                byte[] tempBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(outputPixels.Length);
                try
                {
                    Buffer.BlockCopy(outputPixels, 0, tempBuffer, 0, outputPixels.Length);
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 31; x <= 96; x++)
                        {
                            int sumR = 0, sumG = 0, sumB = 0, count = 0;
                            for (int k = -blurRadius; k <= blurRadius; k++)
                            {
                                int nx = x + k;
                                if (nx is >= 31 and <= 96)
                                {
                                    int nidx = ((y * width) + nx) * 4;
                                    sumB += tempBuffer[nidx];
                                    sumG += tempBuffer[nidx + 1];
                                    sumR += tempBuffer[nidx + 2];
                                    count++;
                                }
                            }
                            int pixelOffset = ((y * width) + x) * 4;
                            outputPixels[pixelOffset] = (byte)(sumB / count);
                            outputPixels[pixelOffset + 1] = (byte)(sumG / count);
                            outputPixels[pixelOffset + 2] = (byte)(sumR / count);
                        }
                    }
                }
                finally
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(tempBuffer);
                }
            }

            BitmapSource source = BitmapSource.Create(
                width, height,
                96, 96,
                PixelFormats.Bgr32, null,
                outputPixels, stride
            );
            source.Freeze();
            return source;
        }
    }
}
