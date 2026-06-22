using System;
using System.IO;

namespace TextureSwapper.Models
{
    public class InGamePaintModel
    {
        public string Name { get; set; } = string.Empty;
        public string TargetUrl { get; set; } = string.Empty;
        public string PreviewImage { get; set; } = string.Empty;

        public Uri? FullPreviewPath
        {
            get
            {
                if (string.IsNullOrEmpty(PreviewImage))
                    return null;
                
                try
                {
                    string fullPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PreviewImage));
                    if (File.Exists(fullPath))
                    {
                        return new Uri(fullPath);
                    }
                }
                catch
                {
                }
                return null;
            }
        }
    }
}
