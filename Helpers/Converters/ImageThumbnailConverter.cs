using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace TextureSwapper.Helpers.Converters
{
    public class ImageThumbnailConverter : IValueConverter
    {
        public object? Convert(
            object? value,
            Type targetType,
            object? parameter,
            CultureInfo culture
        )
        {
            Uri? uri = null;
            if (value is Uri u)
            {
                uri = u;
            }
            else if (value is string path && !string.IsNullOrEmpty(path))
            {
                if (path.StartsWith("pack:", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        uri = new Uri(path, UriKind.Absolute);
                    }
                    catch { }
                }
                else
                {
                    try
                    {
                        uri = new Uri(Path.GetFullPath(path));
                    }
                    catch
                    {
                        try
                        {
                            uri = new Uri(path, UriKind.RelativeOrAbsolute);
                        }
                        catch { }
                    }
                }
            }

            if (uri != null)
            {
                try
                {
                    BitmapImage image = new();
                    image.BeginInit();
                    image.UriSource = uri;
                    image.DecodePixelWidth = 220;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        public object ConvertBack(
            object? value,
            Type targetType,
            object? parameter,
            CultureInfo culture
        )
        {
            throw new NotImplementedException();
        }
    }
}
