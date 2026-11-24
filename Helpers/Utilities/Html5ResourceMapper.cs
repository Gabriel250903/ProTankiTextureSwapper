using System.Text;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Helpers.Utilities
{
    public static class Html5ResourceMapper
    {
        public static bool TryDecodeBase64Target(
            string? base64Target,
            out Html5ResourceTarget? result
        )
        {
            result = null;
            if (string.IsNullOrWhiteSpace(base64Target))
            {
                return false;
            }

            try
            {
                string trimmed = base64Target.Trim();
                int mod = trimmed.Length % 4;
                if (mod == 2)
                {
                    trimmed += "==";
                }
                else if (mod == 3)
                {
                    trimmed += "=";
                }

                byte[] decodedBytes = Convert.FromBase64String(trimmed);
                string url = Encoding.UTF8.GetString(decodedBytes);

                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
                {
                    return false;
                }

                string[] segments = uri.AbsolutePath.Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries
                );
                if (segments.Length < 6)
                {
                    return false;
                }

                long high = Convert.ToInt64(segments[0], 8);
                long b2 = Convert.ToInt64(segments[1], 8);
                long b1 = Convert.ToInt64(segments[2], 8);
                long b0 = Convert.ToInt64(segments[3], 8);
                long version = Convert.ToInt64(segments[4], 8);
                string fileName = Uri.UnescapeDataString(segments[5]);

                long resourceId =
                    (high << 32) | ((b2 & 0xFF) << 16) | ((b1 & 0xFF) << 8) | (b0 & 0xFF);

                result = new Html5ResourceTarget
                {
                    Base64Target = trimmed,
                    OriginalUrl = url,
                    ResourceId = resourceId,
                    Version = version,
                    EntryFileName = fileName,
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string EncodeHtml5Target(long resourceId, string fileName, long version = 0)
        {
            long high = (resourceId >> 32) & 0xFFFFFFFFL;
            long b2 = (resourceId >> 16) & 0xFFL;
            long b1 = (resourceId >> 8) & 0xFFL;
            long b0 = resourceId & 0xFFL;
            string escapedFile = Uri.EscapeDataString(fileName ?? string.Empty);
            string url =
                $"http://146.59.110.103/{Convert.ToString(high, 8)}/{Convert.ToString(b2, 8)}/{Convert.ToString(b1, 8)}/{Convert.ToString(b0, 8)}/{Convert.ToString(version, 8)}/{escapedFile}";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(url));
        }
    }
}
