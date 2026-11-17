using Serilog;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Xml;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services
{
    public class BntArchiveService : IBntArchiveService
    {
        private const uint BntMagic = 0x544E4231;

        public List<TaraEntry> Unpack(ReadOnlySpan<byte> bntData)
        {
            if (bntData.Length < 8)
            {
                throw new InvalidDataException("1BNT archive is too short.");
            }

            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(bntData[..4]);
            if (magic != BntMagic)
            {
                throw new InvalidDataException($"Invalid 1BNT magic: 0x{magic:X8}");
            }

            uint fileCount = BinaryPrimitives.ReadUInt32LittleEndian(bntData.Slice(4, 4));
            List<TaraEntry> entries = new((int)Math.Min(fileCount, 256));

            int offset = 8;
            for (uint i = 0; i < fileCount; i++)
            {
                if (offset + 4 > bntData.Length)
                {
                    throw new InvalidDataException("Unexpected EOF reading 1BNT filename length.");
                }

                int nameLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(
                    bntData.Slice(offset, 4)
                );
                offset += 4;

                if (nameLen < 0 || offset + nameLen + 4 > bntData.Length)
                {
                    throw new InvalidDataException("Unexpected EOF reading 1BNT filename.");
                }

                string fileName = Encoding.UTF8.GetString(bntData.Slice(offset, nameLen));
                offset += nameLen;

                int dataLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(
                    bntData.Slice(offset, 4)
                );
                offset += 4;

                if (dataLen < 0 || offset + dataLen > bntData.Length)
                {
                    throw new InvalidDataException(
                        $"Unexpected EOF reading 1BNT entry '{fileName}' ({dataLen} bytes)."
                    );
                }

                byte[] payload = bntData.Slice(offset, dataLen).ToArray();
                offset += dataLen;

                entries.Add(new TaraEntry { FileName = fileName, Data = payload });
            }

            return entries;
        }

        public byte[] Pack(IReadOnlyList<TaraEntry> entries)
        {
            int totalSize = 8;
            List<byte[]> encodedNames = new(entries.Count);
            foreach (TaraEntry entry in entries)
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(entry.FileName);
                encodedNames.Add(nameBytes);
                totalSize += 4 + nameBytes.Length + 4 + (entry.Data?.Length ?? 0);
            }

            byte[] buffer = new byte[totalSize];
            Span<byte> span = buffer;
            BinaryPrimitives.WriteUInt32LittleEndian(span[..4], BntMagic);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), (uint)entries.Count);

            int offset = 8;
            for (int i = 0; i < entries.Count; i++)
            {
                byte[] nameBytes = encodedNames[i];
                byte[] dataBytes = entries[i].Data ?? [];

                BinaryPrimitives.WriteUInt32LittleEndian(
                    span.Slice(offset, 4),
                    (uint)nameBytes.Length
                );
                offset += 4;

                nameBytes.CopyTo(span.Slice(offset, nameBytes.Length));
                offset += nameBytes.Length;

                BinaryPrimitives.WriteUInt32LittleEndian(
                    span.Slice(offset, 4),
                    (uint)dataBytes.Length
                );
                offset += 4;

                if (dataBytes.Length > 0)
                {
                    dataBytes.CopyTo(span.Slice(offset, dataBytes.Length));
                    offset += dataBytes.Length;
                }
            }

            return buffer;
        }

        public void UpdateModelImagesXml(List<TaraEntry> entries, bool hasAlpha)
        {
            TaraEntry? xmlEntry = entries.FirstOrDefault(e =>
                e.FileName.Equals("images.xml", StringComparison.OrdinalIgnoreCase)
            );
            if (xmlEntry == null || xmlEntry.Data == null || xmlEntry.Data.Length == 0)
            {
                return;
            }

            try
            {
                string xmlText = Encoding.UTF8.GetString(xmlEntry.Data);
                XmlDocument doc = new() { PreserveWhitespace = true };
                doc.LoadXml(xmlText);

                XmlNodeList? imageNodes = doc.SelectNodes("//image");
                if (imageNodes == null)
                {
                    return;
                }

                bool modified = false;
                foreach (XmlNode node in imageNodes)
                {
                    if (node is not XmlElement elem)
                    {
                        continue;
                    }

                    string newName = elem.GetAttribute("new-name");
                    if (newName.Equals("details.jpg", StringComparison.OrdinalIgnoreCase))
                    {
                        if (hasAlpha)
                        {
                            if (
                                !elem.GetAttribute("alpha")
                                    .Equals("details_alpha.jpg", StringComparison.OrdinalIgnoreCase)
                            )
                            {
                                elem.SetAttribute("alpha", "details_alpha.jpg");
                                modified = true;
                            }
                        }
                        else if (elem.HasAttribute("alpha"))
                        {
                            elem.RemoveAttribute("alpha");
                            modified = true;
                        }
                    }
                }

                if (modified)
                {
                    string updatedXml = doc.OuterXml;
                    xmlEntry.Data = Encoding.UTF8.GetBytes(updatedXml);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to update images.xml inside 1BNT archive.");
            }
        }
    }
}
