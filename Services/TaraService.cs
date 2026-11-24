using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using Serilog;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services
{
    public sealed class TaraService : ITaraService
    {
        public List<TaraEntry> Unpack(string taraFilePath)
        {
            if (!File.Exists(taraFilePath))
            {
                throw new FileNotFoundException("Tara archive not found.", taraFilePath);
            }

            using FileStream stream = new(
                taraFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                65536,
                false
            );
            return UnpackStream(stream);
        }

        public async Task<List<TaraEntry>> UnpackAsync(
            string taraFilePath,
            CancellationToken ct = default
        )
        {
            if (!File.Exists(taraFilePath))
            {
                throw new FileNotFoundException("Tara archive not found.", taraFilePath);
            }

            await using FileStream stream = new(
                taraFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                65536,
                true
            );
            return await UnpackStreamAsync(stream, ct);
        }

        public List<TaraEntry> Unpack(byte[] taraData)
        {
            using MemoryStream stream = new(taraData, writable: false);
            return UnpackStream(stream);
        }

        public List<TaraEntry> UnpackStream(Stream sourceStream)
        {
            byte[] headerBuffer = ArrayPool<byte>.Shared.Rent(4);
            try
            {
                sourceStream.ReadExactly(headerBuffer.AsSpan(0, 4));
                uint fileCount = BinaryPrimitives.ReadUInt32BigEndian(headerBuffer.AsSpan(0, 4));

                if (fileCount > 50_000)
                {
                    throw new InvalidDataException($"Unreasonable Tara entry count: {fileCount}");
                }

                Log.Debug("Tara unpack stream: {FileCount} files detected.", fileCount);

                List<(string FileName, uint PayloadSize)> headers = new((int)fileCount);
                for (int i = 0; i < fileCount; i++)
                {
                    sourceStream.ReadExactly(headerBuffer.AsSpan(0, 2));
                    ushort nameLength = BinaryPrimitives.ReadUInt16BigEndian(
                        headerBuffer.AsSpan(0, 2)
                    );

                    byte[] nameBuf = ArrayPool<byte>.Shared.Rent(nameLength);
                    try
                    {
                        sourceStream.ReadExactly(nameBuf.AsSpan(0, nameLength));
                        string rawName = Encoding.UTF8.GetString(nameBuf.AsSpan(0, nameLength));
                        string fileName = Path.GetFileName(rawName.Replace('\\', '/'));
                        if (string.IsNullOrWhiteSpace(fileName))
                        {
                            fileName = $"entry_{i}.bin";
                        }

                        sourceStream.ReadExactly(headerBuffer.AsSpan(0, 4));
                        uint payloadSize = BinaryPrimitives.ReadUInt32BigEndian(
                            headerBuffer.AsSpan(0, 4)
                        );

                        headers.Add((fileName, payloadSize));
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(nameBuf);
                    }
                }

                List<TaraEntry> entries = new((int)fileCount);
                foreach ((string? fileName, uint payloadSize) in headers)
                {
                    byte[] payload = new byte[payloadSize];
                    sourceStream.ReadExactly(payload.AsSpan(0, (int)payloadSize));
                    entries.Add(new TaraEntry { FileName = fileName, Data = payload });
                }

                Log.Debug("Tara unpack stream complete: {Count} entries extracted.", entries.Count);
                return entries;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(headerBuffer);
            }
        }

        public async Task<List<TaraEntry>> UnpackStreamAsync(
            Stream sourceStream,
            CancellationToken ct = default
        )
        {
            byte[] headerBuffer = ArrayPool<byte>.Shared.Rent(4);
            try
            {
                await sourceStream.ReadExactlyAsync(headerBuffer.AsMemory(0, 4), ct);
                uint fileCount = BinaryPrimitives.ReadUInt32BigEndian(headerBuffer.AsSpan(0, 4));

                if (fileCount > 50_000)
                {
                    throw new InvalidDataException($"Unreasonable Tara entry count: {fileCount}");
                }

                Log.Debug("Tara unpack stream async: {FileCount} files detected.", fileCount);

                List<(string FileName, uint PayloadSize)> headers = new((int)fileCount);
                for (int i = 0; i < fileCount; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    await sourceStream.ReadExactlyAsync(headerBuffer.AsMemory(0, 2), ct);
                    ushort nameLength = BinaryPrimitives.ReadUInt16BigEndian(
                        headerBuffer.AsSpan(0, 2)
                    );

                    byte[] nameBuf = ArrayPool<byte>.Shared.Rent(nameLength);
                    try
                    {
                        await sourceStream.ReadExactlyAsync(nameBuf.AsMemory(0, nameLength), ct);
                        string rawName = Encoding.UTF8.GetString(nameBuf.AsSpan(0, nameLength));
                        string fileName = Path.GetFileName(rawName.Replace('\\', '/'));
                        if (string.IsNullOrWhiteSpace(fileName))
                        {
                            fileName = $"entry_{i}.bin";
                        }

                        await sourceStream.ReadExactlyAsync(headerBuffer.AsMemory(0, 4), ct);
                        uint payloadSize = BinaryPrimitives.ReadUInt32BigEndian(
                            headerBuffer.AsSpan(0, 4)
                        );

                        headers.Add((fileName, payloadSize));
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(nameBuf);
                    }
                }

                List<TaraEntry> entries = new((int)fileCount);
                foreach ((string? fileName, uint payloadSize) in headers)
                {
                    ct.ThrowIfCancellationRequested();
                    byte[] payload = new byte[payloadSize];
                    await sourceStream.ReadExactlyAsync(payload.AsMemory(0, (int)payloadSize), ct);
                    entries.Add(new TaraEntry { FileName = fileName, Data = payload });
                }

                Log.Debug(
                    "Tara unpack stream async complete: {Count} entries extracted.",
                    entries.Count
                );
                return entries;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(headerBuffer);
            }
        }

        public void Pack(List<TaraEntry> entries, string outputPath)
        {
            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            string tempPath = outputPath + $".tmp_{Guid.NewGuid():N}";
            using (
                FileStream fs = new(
                    tempPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    65536,
                    false
                )
            )
            {
                PackStream(entries, fs);
            }

            File.Move(tempPath, outputPath, overwrite: true);
            Log.Debug("Tara packed {Count} entries to {Path}.", entries.Count, outputPath);
        }

        public async Task PackAsync(
            List<TaraEntry> entries,
            string outputPath,
            CancellationToken ct = default
        )
        {
            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            string tempPath = outputPath + $".tmp_{Guid.NewGuid():N}";
            await using (
                FileStream fs = new(
                    tempPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    65536,
                    true
                )
            )
            {
                await PackStreamAsync(entries, fs, ct);
            }

            File.Move(tempPath, outputPath, overwrite: true);
            Log.Debug("Tara async packed {Count} entries to {Path}.", entries.Count, outputPath);
        }

        public void PackStream(List<TaraEntry> entries, Stream destStream)
        {
            byte[] headerBuffer = ArrayPool<byte>.Shared.Rent(4);
            try
            {
                BinaryPrimitives.WriteUInt32BigEndian(
                    headerBuffer.AsSpan(0, 4),
                    (uint)entries.Count
                );
                destStream.Write(headerBuffer.AsSpan(0, 4));

                foreach (TaraEntry entry in entries)
                {
                    byte[] nameBytes = Encoding.UTF8.GetBytes(entry.FileName);
                    BinaryPrimitives.WriteUInt16BigEndian(
                        headerBuffer.AsSpan(0, 2),
                        (ushort)nameBytes.Length
                    );
                    destStream.Write(headerBuffer.AsSpan(0, 2));
                    destStream.Write(nameBytes);

                    BinaryPrimitives.WriteUInt32BigEndian(
                        headerBuffer.AsSpan(0, 4),
                        (uint)entry.Data.Length
                    );
                    destStream.Write(headerBuffer.AsSpan(0, 4));
                }

                foreach (TaraEntry entry in entries)
                {
                    destStream.Write(entry.Data);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(headerBuffer);
            }
        }

        public async Task PackStreamAsync(
            List<TaraEntry> entries,
            Stream destStream,
            CancellationToken ct = default
        )
        {
            byte[] headerBuffer = ArrayPool<byte>.Shared.Rent(4);
            try
            {
                BinaryPrimitives.WriteUInt32BigEndian(
                    headerBuffer.AsSpan(0, 4),
                    (uint)entries.Count
                );
                await destStream.WriteAsync(headerBuffer.AsMemory(0, 4), ct);

                foreach (TaraEntry entry in entries)
                {
                    ct.ThrowIfCancellationRequested();
                    byte[] nameBytes = Encoding.UTF8.GetBytes(entry.FileName);
                    BinaryPrimitives.WriteUInt16BigEndian(
                        headerBuffer.AsSpan(0, 2),
                        (ushort)nameBytes.Length
                    );
                    await destStream.WriteAsync(headerBuffer.AsMemory(0, 2), ct);
                    await destStream.WriteAsync(nameBytes.AsMemory(), ct);

                    BinaryPrimitives.WriteUInt32BigEndian(
                        headerBuffer.AsSpan(0, 4),
                        (uint)entry.Data.Length
                    );
                    await destStream.WriteAsync(headerBuffer.AsMemory(0, 4), ct);
                }

                foreach (TaraEntry entry in entries)
                {
                    ct.ThrowIfCancellationRequested();
                    await destStream.WriteAsync(entry.Data.AsMemory(), ct);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(headerBuffer);
            }
        }

        public byte[] Pack(List<TaraEntry> entries)
        {
            using MemoryStream ms = new();
            PackStream(entries, ms);
            return ms.ToArray();
        }
    }
}
