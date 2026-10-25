using System.IO;
using System.Text;

namespace TextureSwapper.Helpers.Utilities
{
    public static class Mesh3dsHelper
    {
        public static HashSet<string> ExtractTextureNames(byte[]? meshBytes)
        {
            HashSet<string> textures = new(StringComparer.OrdinalIgnoreCase);
            if (meshBytes == null || meshBytes.Length < 6)
            {
                return textures;
            }

            ParseChunks(meshBytes, 0, meshBytes.Length, textures);
            return textures;
        }

        private static void ParseChunks(byte[] data, int offset, int end, HashSet<string> textures)
        {
            while (offset + 6 <= end)
            {
                ushort chunkId = BitConverter.ToUInt16(data, offset);
                uint chunkSize = BitConverter.ToUInt32(data, offset + 2);

                if (chunkSize < 6 || offset + chunkSize > (uint)end)
                {
                    break;
                }

                if (chunkId == 0xA300)
                {
                    int maxStrLen = (int)chunkSize - 6;
                    int strLen = 0;
                    while (strLen < maxStrLen && data[offset + 6 + strLen] != 0)
                    {
                        strLen++;
                    }

                    if (strLen > 0)
                    {
                        string rawName = Encoding.ASCII.GetString(data, offset + 6, strLen).Trim();
                        string fileName = Path.GetFileName(rawName);
                        if (!string.IsNullOrWhiteSpace(fileName) && IsValidFileName(fileName))
                        {
                            _ = textures.Add(fileName);
                        }
                    }
                }
                else if (IsContainerChunk(chunkId))
                {
                    int subOffset = offset + 6;
                    if (chunkId == 0x4000)
                    {
                        while (subOffset < offset + chunkSize && data[subOffset] != 0)
                        {
                            subOffset++;
                        }
                        subOffset++;
                    }

                    if (subOffset < offset + chunkSize)
                    {
                        ParseChunks(data, subOffset, offset + (int)chunkSize, textures);
                    }
                }

                offset += (int)chunkSize;
            }
        }

        private static bool IsContainerChunk(ushort id)
        {
            return id
                is 0x4D4D
                    or 0x3D3D
                    or 0xAFFF
                    or 0xA200
                    or 0xA33A
                    or 0xA210
                    or 0xA230
                    or 0xA204
                    or 0xA220
                    or 0xA33C
                    or 0xA33D
                    or 0x4000
                    or 0x4100;
        }

        private static bool IsValidFileName(string name)
        {
            foreach (char c in name)
            {
                if (c < 32 || c > 126 || Path.GetInvalidFileNameChars().Contains(c))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
