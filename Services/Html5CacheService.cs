using Serilog;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using ZstdSharp;

namespace TextureSwapper.Services
{
    public partial class Html5CacheService(
        IBntArchiveService bntArchiveService,
        ISettingsService? settingsService = null
    ) : IHtml5CacheService
    {
        private const string LevelDbFolderName = "https_play-protanki.com_0.indexeddb.leveldb";
        private const string BlobFolderName = "https_play-protanki.com_0.indexeddb.blob";
        private const int LogBlockSize = 32768;

        private const byte ObjectStoreDataEntryType = 0x01;
        private const byte BlobEntryType = 0x03;
        
        private static readonly byte[] BytesFieldTag =
        [
            0x22,
            0x05,
            (byte)'b',
            (byte)'y',
            (byte)'t',
            (byte)'e',
            (byte)'s',
        ];
        private static readonly byte[] UpdatedAtFieldTag =
        [
            0x22,
            0x09,
            (byte)'u',
            (byte)'p',
            (byte)'d',
            (byte)'a',
            (byte)'t',
            (byte)'e',
            (byte)'d',
            (byte)'A',
            (byte)'t',
        ];
        private static readonly byte[] DataFieldTag =
        [
            0x22,
            0x04,
            (byte)'d',
            (byte)'a',
            (byte)'t',
            (byte)'a',
            (byte)'B',
        ];
        private static readonly byte[] ObjectTrailer = [0x7B, 0x08];
        private static readonly byte[] WrapperMimeUtf16Be =
            Encoding.BigEndianUnicode.GetBytes("application/vnd.blink-idb-value-wrapper");

        private readonly IBntArchiveService _bntArchiveService = bntArchiveService;
        private readonly object _syncLock = new();

        private string? _cachedLevelDbDir;
        private string? _cachedLevelDbSnapshot;
        private Dictionary<string, ResourceRecord>? _cachedRecords;
        private ulong _maxSequenceNumber;
        private string? _activeLogFilePath;

        private string? _cachedCacheDataDir;
        private string? _cachedCacheDataSnapshot;
        private string? _cachedCoreBundlePath;
        private string? _cachedGarageBundlePath;
        private string? _cachedHudBundlePath;
        private readonly Dictionary<string, (long Length, long LastWriteTicks, bool IsCore, bool IsGarage, bool IsHud)> _bundleIndexCache =
            new(StringComparer.OrdinalIgnoreCase);

        private sealed class ResourceRecord
        {
            public required string Key { get; init; }
            public required byte[] RawUserKey { get; init; }
            public byte DatabaseId { get; init; } = 1;
            public ulong IdbVersion { get; set; }
            public bool IsExternalBlob { get; set; }
            public ulong BlobNumber { get; set; }
            public byte[] DataValueBytes { get; set; } = [];
            public byte[]? BlobEntryValueBytes { get; set; }
        }

        public void InvalidateCache()
        {
            lock (_syncLock)
            {
                _cachedRecords = null;
                _cachedLevelDbDir = null;
                _cachedLevelDbSnapshot = null;
                _cachedCacheDataDir = null;
                _cachedCacheDataSnapshot = null;
                _cachedCoreBundlePath = null;
                _cachedGarageBundlePath = null;
                _cachedHudBundlePath = null;
                _bundleIndexCache.Clear();
            }
        }

        public bool TryResolveHtml5Paths(
            string cacheRootPath,
            out string levelDbDir,
            out string blobDir
        )
        {
            levelDbDir = string.Empty;
            blobDir = string.Empty;

            if (string.IsNullOrWhiteSpace(cacheRootPath))
            {
                return false;
            }

            string trimmed = cacheRootPath.Trim();

            string candidateLdb1 = Path.Combine(trimmed, "IndexedDB", LevelDbFolderName);
            string candidateBlob1 = Path.Combine(trimmed, "IndexedDB", BlobFolderName);
            if (Directory.Exists(candidateLdb1))
            {
                levelDbDir = candidateLdb1;
                blobDir = candidateBlob1;
                return true;
            }

            string candidateLdb2 = Path.Combine(trimmed, LevelDbFolderName);
            string candidateBlob2 = Path.Combine(trimmed, BlobFolderName);
            if (Directory.Exists(candidateLdb2))
            {
                levelDbDir = candidateLdb2;
                blobDir = candidateBlob2;
                return true;
            }

            if (
                Directory.Exists(trimmed)
                && Path.GetFileName(trimmed)
                    .Equals(LevelDbFolderName, StringComparison.OrdinalIgnoreCase)
            )
            {
                levelDbDir = trimmed;
                string parent = Path.GetDirectoryName(trimmed) ?? trimmed;
                blobDir = Path.Combine(parent, BlobFolderName);
                return true;
            }

            return false;
        }

        public bool ResourceExists(string cacheRootPath, long resourceId)
        {
            if (
                !TryResolveHtml5Paths(cacheRootPath, out string levelDbDir, out string blobDir)
            )
            {
                return false;
            }

            lock (_syncLock)
            {
                EnsureIndexLoaded(levelDbDir);
                string key = $"{resourceId}:default";
                if (
                    _cachedRecords == null
                    || !_cachedRecords.TryGetValue(key, out ResourceRecord? rec)
                )
                {
                    return false;
                }

                if (rec.IsExternalBlob)
                {
                    string blobPath = GetBlobFilePath(blobDir, rec.DatabaseId, rec.BlobNumber);
                    return File.Exists(blobPath);
                }

                return rec.DataValueBytes.Length > 0;
            }
        }

        public byte[]? ReadRawBntBytes(string cacheRootPath, long resourceId)
        {
            if (
                !TryResolveHtml5Paths(cacheRootPath, out string levelDbDir, out string blobDir)
            )
            {
                return null;
            }

            lock (_syncLock)
            {
                EnsureIndexLoaded(levelDbDir);
                string key = $"{resourceId}:default";
                if (
                    _cachedRecords == null
                    || !_cachedRecords.TryGetValue(key, out ResourceRecord? rec)
                )
                {
                    return null;
                }

                byte[]? v8Bytes = ReadUncompressedV8Payload(rec, blobDir);
                if (v8Bytes == null)
                {
                    return null;
                }

                int dataTagIdx = IndexOfBytes(v8Bytes, DataFieldTag, 0);
                if (dataTagIdx < 0)
                {
                    return null;
                }

                int pos = dataTagIdx + DataFieldTag.Length;
                if (
                    !TryReadVarint(v8Bytes, ref pos, out ulong bntLen)
                    || bntLen > (ulong)(v8Bytes.Length - pos)
                )
                {
                    return null;
                }

                byte[] bnt = new byte[(int)bntLen];
                Buffer.BlockCopy(v8Bytes, pos, bnt, 0, (int)bntLen);
                return bnt;
            }
        }

        public List<TaraEntry>? ReadResourceEntries(string cacheRootPath, long resourceId)
        {
            byte[]? rawBnt = ReadRawBntBytes(cacheRootPath, resourceId);
            return rawBnt == null ? null : _bntArchiveService.Unpack(rawBnt);
        }

        public bool WriteResourceEntries(
            string cacheRootPath,
            long resourceId,
            IReadOnlyList<TaraEntry> entries
        )
        {
            byte[] bntBytes = _bntArchiveService.Pack(entries);
            return WriteRawBntBytes(cacheRootPath, resourceId, bntBytes);
        }

        public bool WriteRawBntBytes(
            string cacheRootPath,
            long resourceId,
            ReadOnlySpan<byte> bntBytes
        )
        {
            if (
                !TryResolveHtml5Paths(cacheRootPath, out string levelDbDir, out string blobDir)
                || IsLevelDbLocked(levelDbDir)
            )
            {
                return false;
            }

            lock (_syncLock)
            {
                EnsureIndexLoaded(levelDbDir);
                string key = $"{resourceId}:default";
                if (
                    _cachedRecords == null
                    || !_cachedRecords.TryGetValue(key, out ResourceRecord? rec)
                )
                {
                    return false;
                }

                byte[]? existingV8 = ReadUncompressedV8Payload(rec, blobDir);
                if (existingV8 == null)
                {
                    return false;
                }

                byte[]? updatedV8 = RebuildV8Payload(existingV8, bntBytes);
                if (updatedV8 == null)
                {
                    return false;
                }

                if (rec.IsExternalBlob)
                {
                    string blobPath = GetBlobFilePath(blobDir, rec.DatabaseId, rec.BlobNumber);
                    string? parentDir = Path.GetDirectoryName(blobPath);
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        _ = Directory.CreateDirectory(parentDir);
                    }

                    string tempBlobPath = $"{blobPath}.tmp_{Guid.NewGuid():N}";
                    string? backupBlobPath = File.Exists(blobPath)
                        ? $"{blobPath}.bak_{Guid.NewGuid():N}"
                        : null;
                    bool blobReplaced = false;

                    try
                    {
                        using (
                            FileStream bfs = new(
                                tempBlobPath,
                                FileMode.CreateNew,
                                FileAccess.Write,
                                FileShare.None
                            )
                        )
                        {
                            bfs.Write(updatedV8);
                            bfs.Flush(true);
                        }

                        if (backupBlobPath != null)
                        {
                            File.Copy(blobPath, backupBlobPath, overwrite: true);
                        }

                        File.Move(tempBlobPath, blobPath, overwrite: true);
                        blobReplaced = true;

                        using MemoryStream dataValMs = new();
                        WriteVarint(dataValMs, rec.IdbVersion);
                        dataValMs.WriteByte(0xFF);
                        dataValMs.WriteByte(0x11);
                        dataValMs.WriteByte(0x01);
                        WriteVarint(dataValMs, (ulong)updatedV8.Length);
                        dataValMs.WriteByte(0x00);
                        byte[] newDataVal = dataValMs.ToArray();

                        using MemoryStream blobValMs = new();
                        blobValMs.WriteByte(0x00);
                        WriteVarint(blobValMs, rec.BlobNumber);
                        WriteVarint(blobValMs, (ulong)(WrapperMimeUtf16Be.Length / 2));
                        blobValMs.Write(WrapperMimeUtf16Be);
                        WriteVarint(blobValMs, (ulong)updatedV8.Length);
                        byte[] newBlobVal = blobValMs.ToArray();

                        byte[] blobEntryPrefix =
                        [
                            0x00,
                            rec.DatabaseId,
                            rec.RawUserKey.Length > 2 ? rec.RawUserKey[2] : (byte)0x01,
                            BlobEntryType,
                            0x01,
                        ];
                        byte[] blobEntryUserKey = BuildUserKey(blobEntryPrefix, rec.Key);

                        AppendWriteBatchToWal(
                            levelDbDir,
                            [
                                (rec.RawUserKey, newDataVal),
                                (blobEntryUserKey, newBlobVal),
                            ]
                        );

                        rec.DataValueBytes = newDataVal;
                        rec.BlobEntryValueBytes = newBlobVal;

                        if (backupBlobPath != null && File.Exists(backupBlobPath))
                        {
                            try
                            {
                                File.Delete(backupBlobPath);
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to write external blob and WAL metadata for resource {ResourceId}", resourceId);
                        if (blobReplaced)
                        {
                            try
                            {
                                if (backupBlobPath != null && File.Exists(backupBlobPath))
                                {
                                    File.Move(backupBlobPath, blobPath, overwrite: true);
                                }
                                else if (File.Exists(blobPath))
                                {
                                    File.Delete(blobPath);
                                }
                            }
                            catch (Exception rbEx)
                            {
                                Log.Error(rbEx, "Failed to restore blob {BlobPath} after WAL failure", blobPath);
                            }
                        }

                        if (File.Exists(tempBlobPath))
                        {
                            try
                            {
                                File.Delete(tempBlobPath);
                            }
                            catch { }
                        }

                        _cachedRecords = null;
                        _cachedLevelDbSnapshot = null;
                        return false;
                    }
                }
                else
                {
                    try
                    {
                        using MemoryStream inlineValMs = new(updatedV8.Length + 8);
                        WriteVarint(inlineValMs, rec.IdbVersion);
                        inlineValMs.Write(updatedV8);
                        byte[] newInlineVal = inlineValMs.ToArray();

                        AppendWriteBatchToWal(levelDbDir, [(rec.RawUserKey, newInlineVal)]);
                        rec.DataValueBytes = newInlineVal;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to write inline WAL metadata for resource {ResourceId}", resourceId);
                        _cachedRecords = null;
                        _cachedLevelDbSnapshot = null;
                        return false;
                    }
                }

                _cachedRecords = null;
                _cachedLevelDbSnapshot = null;
                byte[]? verified = ReadRawBntBytes(cacheRootPath, resourceId);
                return verified != null && verified.AsSpan().SequenceEqual(bntBytes);
            }
        }

        private static string GetBlobFilePath(string blobDir, byte databaseId, ulong blobNumber)
        {
            ulong folderByte = (blobNumber >> 8) & 0xFF;
            return Path.Combine(
                blobDir,
                $"{databaseId}",
                $"{folderByte:x2}",
                $"{blobNumber:x}"
            );
        }

        private static byte[]? ReadUncompressedV8Payload(ResourceRecord rec, string blobDir)
        {
            byte[] rawV8;
            if (rec.IsExternalBlob)
            {
                string blobPath = GetBlobFilePath(blobDir, rec.DatabaseId, rec.BlobNumber);
                if (!File.Exists(blobPath))
                {
                    return null;
                }

                rawV8 = File.ReadAllBytes(blobPath);
            }
            else
            {
                int pos = 0;
                if (!TryReadVarint(rec.DataValueBytes, ref pos, out _))
                {
                    return null;
                }
                rawV8 = rec.DataValueBytes[pos..];
            }

            return rawV8.Length >= 3
                && rawV8[0] == 0xFF
                && rawV8[1] == 0x11
                && rawV8[2] == 0x02
                ? SnappyDecompress(rawV8.AsSpan(3))
                : rawV8;
        }

        private static byte[]? RebuildV8Payload(
            byte[] existingV8,
            ReadOnlySpan<byte> newBntBytes
        )
        {
            int bytesTagIdx = IndexOfBytes(existingV8, BytesFieldTag, 0);
            if (bytesTagIdx < 0)
            {
                return null;
            }

            int updatedAtIdx = IndexOfBytes(
                existingV8,
                UpdatedAtFieldTag,
                bytesTagIdx + BytesFieldTag.Length
            );
            if (updatedAtIdx < 0)
            {
                return null;
            }

            int dataTagIdx = IndexOfBytes(
                existingV8,
                DataFieldTag,
                updatedAtIdx + UpdatedAtFieldTag.Length
            );
            if (dataTagIdx < 0)
            {
                return null;
            }

            using MemoryStream ms = new(existingV8.Length + newBntBytes.Length);

            int prefixLen = bytesTagIdx + BytesFieldTag.Length;
            ms.Write(existingV8, 0, prefixLen);

            ms.WriteByte((byte)'I');
            WriteVarint(ms, (ulong)newBntBytes.Length << 1);

            int middleLen = dataTagIdx + DataFieldTag.Length - updatedAtIdx;
            ms.Write(existingV8, updatedAtIdx, middleLen);

            WriteVarint(ms, (ulong)newBntBytes.Length);
            ms.Write(newBntBytes);
            ms.Write(ObjectTrailer);

            return ms.ToArray();
        }

        private void EnsureIndexLoaded(string levelDbDir)
        {
            string snapshotKey = GetDirectorySnapshotKey(levelDbDir);
            if (
                _cachedRecords != null
                && string.Equals(
                    _cachedLevelDbDir,
                    levelDbDir,
                    StringComparison.OrdinalIgnoreCase
                )
                && string.Equals(_cachedLevelDbSnapshot, snapshotKey, StringComparison.Ordinal)
            )
            {
                return;
            }

            List<(ulong Seq, byte ValueType, byte[] UserKey, byte[] Value)> allEntries = [];
            string? latestLogFile = null;

            foreach (string file in Directory.GetFiles(levelDbDir).OrderBy(f => f))
            {
                string ext = Path.GetExtension(file);
                if (ext.Equals(".ldb", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".sst", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        ParseLdbFile(file, allEntries);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to parse LevelDB SSTable {File}", file);
                    }
                }
                else if (ext.Equals(".log", StringComparison.OrdinalIgnoreCase))
                {
                    latestLogFile = file;
                    try
                    {
                        ParseLogFile(file, allEntries);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to parse LevelDB WAL {File}", file);
                    }
                }
            }

            allEntries.Sort((a, b) => a.Seq.CompareTo(b.Seq));

            ulong maxSeq = allEntries.Count > 0 ? allEntries[^1].Seq : 1;
            Dictionary<(byte DbId, byte StoreId, string Key), (byte[] RawKey, byte[] Val)> dataMap =
                [];
            Dictionary<(byte DbId, byte StoreId, string Key), byte[]> blobMap = [];

            foreach ((_, byte vtype, byte[] ukey, byte[] val) in allEntries)
            {
                if (
                    ukey.Length >= 5
                    && ukey[0] == 0x00
                    && ukey[1] >= 1
                    && ukey[2] >= 1
                    && ukey[4] == 0x01
                )
                {
                    byte dbId = ukey[1];
                    byte storeId = ukey[2];
                    byte entryType = ukey[3];

                    if (entryType == ObjectStoreDataEntryType)
                    {
                        string? keyStr = DecodeIdbStringKey(ukey, 5);
                        if (keyStr != null)
                        {
                            (byte dbId, byte storeId, string keyStr) composite = (dbId, storeId, keyStr);
                            if (vtype == 1)
                            {
                                dataMap[composite] = (ukey, val);
                            }
                            else if (vtype == 0)
                            {
                                _ = dataMap.Remove(composite);
                            }
                        }
                    }
                    else if (entryType == BlobEntryType)
                    {
                        string? keyStr = DecodeIdbStringKey(ukey, 5);
                        if (keyStr != null)
                        {
                            (byte dbId, byte storeId, string keyStr) composite = (dbId, storeId, keyStr);
                            if (vtype == 1)
                            {
                                blobMap[composite] = val;
                            }
                            else if (vtype == 0)
                            {
                                _ = blobMap.Remove(composite);
                            }
                        }
                    }
                }
            }

            Dictionary<string, ResourceRecord> records = new(
                dataMap.Count,
                StringComparer.Ordinal
            );

            foreach (
                (
                    (byte dbId, byte storeId, string keyStr),
                    (byte[] rawKey, byte[] val)
                ) in dataMap
            )
            {
                if (val.Length == 0)
                {
                    continue;
                }

                int pos = 0;
                if (!TryReadVarint(val, ref pos, out ulong idbVer))
                {
                    continue;
                }
                ReadOnlySpan<byte> rem = val.AsSpan(pos);

                bool isBlob =
                    rem.Length >= 3 && rem[0] == 0xFF && rem[1] == 0x11 && rem[2] == 0x01;
                ulong blobNum = 0;
                byte[]? blobEntryVal = null;

                if (isBlob)
                {
                    if (
                        !blobMap.TryGetValue((dbId, storeId, keyStr), out blobEntryVal)
                        || blobEntryVal == null
                        || blobEntryVal.Length <= 1
                        || blobEntryVal[0] != 0x00
                    )
                    {
                        continue;
                    }

                    int bpos = 1;
                    if (!TryReadVarint(blobEntryVal, ref bpos, out blobNum) || blobNum == 0)
                    {
                        continue;
                    }
                }

                records[keyStr] = new ResourceRecord
                {
                    Key = keyStr,
                    RawUserKey = rawKey,
                    DatabaseId = dbId,
                    IdbVersion = idbVer,
                    IsExternalBlob = isBlob,
                    BlobNumber = blobNum,
                    DataValueBytes = val,
                    BlobEntryValueBytes = blobEntryVal,
                };
            }

            _cachedRecords = records;
            _cachedLevelDbDir = levelDbDir;
            _cachedLevelDbSnapshot = snapshotKey;
            _maxSequenceNumber = maxSeq;
            _activeLogFilePath = latestLogFile;
        }

        private void AppendWriteBatchToWal(
            string levelDbDir,
            IReadOnlyList<(byte[] UserKey, byte[] Value)> entries
        )
        {
            if (entries.Count == 0)
            {
                return;
            }

            string logPath =
                _activeLogFilePath
                ?? Directory
                    .GetFiles(levelDbDir, "*.log")
                    .OrderBy(f => f)
                    .LastOrDefault()
                ?? Path.Combine(levelDbDir, "000012.log");

            ulong startSeq = _maxSequenceNumber + 1;
            _maxSequenceNumber += (ulong)entries.Count;

            using MemoryStream batchMs = new();
            Span<byte> hdr = stackalloc byte[12];
            BinaryPrimitives.WriteUInt64LittleEndian(hdr[..8], startSeq);
            BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(8, 4), (uint)entries.Count);
            batchMs.Write(hdr);

            foreach ((byte[] ukey, byte[] val) in entries)
            {
                batchMs.WriteByte(1);
                WriteVarint(batchMs, (ulong)ukey.Length);
                batchMs.Write(ukey);
                WriteVarint(batchMs, (ulong)val.Length);
                batchMs.Write(val);
            }

            ReadOnlySpan<byte> payload = batchMs.ToArray();

            using FileStream fs = new(
                logPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read
            );
            if (fs.Length > 0)
            {
                byte[] existingLog = new byte[fs.Length];
                fs.ReadExactly(existingLog);
                int validLen = FindLastValidLogOffset(existingLog);
                if (validLen < existingLog.Length)
                {
                    Log.Warning(
                        "Truncating incomplete/corrupt LevelDB WAL tail in {LogPath} from {OldLen} to {ValidLen} bytes.",
                        logPath,
                        existingLog.Length,
                        validLen
                    );
                    fs.SetLength(validLen);
                }
                _ = fs.Seek(validLen, SeekOrigin.Begin);
            }
            else
            {
                _ = fs.Seek(0, SeekOrigin.End);
            }

            int offset = 0;
            bool isFirst = true;
            Span<byte> zeroPad = stackalloc byte[7];
            zeroPad.Clear();
            Span<byte> recHeader = stackalloc byte[7];

            while (offset < payload.Length)
            {
                int blockOffset = (int)(fs.Position % LogBlockSize);
                int blockRemaining = LogBlockSize - blockOffset;

                if (blockRemaining < 7)
                {
                    fs.Write(zeroPad[..blockRemaining]);
                    blockRemaining = LogBlockSize;
                }

                int maxChunk = blockRemaining - 7;
                int chunkLen = Math.Min(payload.Length - offset, maxChunk);
                bool isLast = (offset + chunkLen) == payload.Length;

                byte recordType =
                    (isFirst && isLast) ? (byte)1
                    : isFirst ? (byte)2
                    : isLast ? (byte)4
                    : (byte)3;

                ReadOnlySpan<byte> chunk = payload.Slice(offset, chunkLen);
                uint maskedCrc = ComputeMaskedCrc32C(recordType, chunk);

                BinaryPrimitives.WriteUInt32LittleEndian(recHeader[..4], maskedCrc);
                BinaryPrimitives.WriteUInt16LittleEndian(recHeader.Slice(4, 2), (ushort)chunkLen);
                recHeader[6] = recordType;

                fs.Write(recHeader);
                fs.Write(chunk);

                offset += chunkLen;
                isFirst = false;
            }

            fs.Flush(true);
        }

        private static readonly uint[] Crc32cTable = BuildCrc32cTable();

        private static uint[] BuildCrc32cTable()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0x82F63B78u : crc >> 1;
                }
                table[i] = crc;
            }
            return table;
        }

        private static uint ComputeMaskedCrc32C(byte recordType, ReadOnlySpan<byte> data)
        {
            uint crc = 0xFFFFFFFFu;
            crc = Crc32cTable[(crc ^ recordType) & 0xFF] ^ (crc >> 8);
            for (int i = 0; i < data.Length; i++)
            {
                crc = Crc32cTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            }
            crc ^= 0xFFFFFFFFu;
            return ((crc >> 15) | (crc << 17)) + 0xA282EAD8u;
        }

        private static void ParseLdbFile(
            string filePath,
            List<(ulong Seq, byte ValueType, byte[] UserKey, byte[] Value)> output
        )
        {
            byte[] data = File.ReadAllBytes(filePath);
            if (data.Length < 48)
            {
                return;
            }

            int footerStart = data.Length - 48;
            ulong magic = BinaryPrimitives.ReadUInt64LittleEndian(
                data.AsSpan(footerStart + 40, 8)
            );
            if (magic != 0xDB4775248B80FB57UL)
            {
                return;
            }

            int pos = footerStart;
            if (
                !TryReadVarint(data, ref pos, out _)
                || !TryReadVarint(data, ref pos, out _)
                || !TryReadVarint(data, ref pos, out ulong indexOffset)
                || !TryReadVarint(data, ref pos, out ulong indexSize)
            )
            {
                return;
            }
            if (indexOffset > (ulong)footerStart || indexSize > (ulong)footerStart - indexOffset)
            {
                return;
            }

            byte[] indexBlock = ReadSstBlock(data, (int)indexOffset, (int)indexSize);
            foreach ((_, byte[] indexVal) in EnumerateSstBlockEntries(indexBlock))
            {
                int ipos = 0;
                if (
                    !TryReadVarint(indexVal, ref ipos, out ulong blockOffset)
                    || !TryReadVarint(indexVal, ref ipos, out ulong blockSize)
                )
                {
                    continue;
                }
                if (blockOffset > (ulong)footerStart || blockSize > (ulong)footerStart - blockOffset)
                {
                    continue;
                }

                byte[] dataBlock = ReadSstBlock(data, (int)blockOffset, (int)blockSize);
                foreach ((byte[] internalKey, byte[] val) in EnumerateSstBlockEntries(dataBlock))
                {
                    if (internalKey.Length < 8)
                    {
                        continue;
                    }

                    byte[] userKey = internalKey[..^8];
                    ulong seqAndType = BinaryPrimitives.ReadUInt64LittleEndian(
                        internalKey.AsSpan(internalKey.Length - 8, 8)
                    );
                    byte vtype = (byte)(seqAndType & 0xFF);
                    ulong seq = seqAndType >> 8;
                    output.Add((seq, vtype, userKey, val));
                }
            }
        }

        private static byte[] ReadSstBlock(byte[] fileData, int offset, int size)
        {
            if (offset < 0 || size < 0 || offset + size >= fileData.Length)
            {
                throw new InvalidDataException("SST block offset/size out of bounds.");
            }

            byte compressionType = fileData[offset + size];
            ReadOnlySpan<byte> raw = fileData.AsSpan(offset, size);
            return compressionType switch
            {
                0 => raw.ToArray(),
                1 => SnappyDecompress(raw),
                _ => throw new InvalidDataException(
                    $"Unsupported SST block compression type: {compressionType}"
                ),
            };
        }

        private static List<(byte[] Key, byte[] Value)> EnumerateSstBlockEntries(byte[] block)
        {
            List<(byte[] Key, byte[] Value)> result = [];
            if (block.Length < 4)
            {
                return result;
            }

            uint numRestarts = BinaryPrimitives.ReadUInt32LittleEndian(
                block.AsSpan(block.Length - 4, 4)
            );
            int limit = block.Length - (int)(4 * (numRestarts + 1));
            int pos = 0;
            byte[] prevKey = [];

            while (pos < limit)
            {
                if (
                    !TryReadVarint(block, ref pos, out ulong sharedRaw)
                    || !TryReadVarint(block, ref pos, out ulong nonSharedRaw)
                    || !TryReadVarint(block, ref pos, out ulong valLenRaw)
                )
                {
                    break;
                }

                if (
                    sharedRaw > (ulong)prevKey.Length
                    || nonSharedRaw > (ulong)(limit - pos)
                    || valLenRaw > (ulong)(limit - pos) - nonSharedRaw
                )
                {
                    break;
                }

                int shared = (int)sharedRaw;
                int nonShared = (int)nonSharedRaw;
                int valLen = (int)valLenRaw;

                byte[] key = new byte[shared + nonShared];
                if (shared > 0)
                {
                    Buffer.BlockCopy(prevKey, 0, key, 0, shared);
                }
                Buffer.BlockCopy(block, pos, key, shared, nonShared);
                pos += nonShared;

                byte[] val = new byte[valLen];
                Buffer.BlockCopy(block, pos, val, 0, valLen);
                pos += valLen;

                prevKey = key;
                result.Add((key, val));
            }

            return result;
        }

        private static int FindLastValidLogOffset(ReadOnlySpan<byte> data)
        {
            int pos = 0;
            int n = data.Length;
            int lastValidOffset = 0;
            bool inFragment = false;

            while (pos < n)
            {
                int blockRem = LogBlockSize - (pos % LogBlockSize);
                if (blockRem < 7)
                {
                    if (pos + blockRem > n)
                    {
                        break;
                    }
                    pos += blockRem;
                    if (!inFragment)
                    {
                        lastValidOffset = pos;
                    }
                    continue;
                }

                if (pos + 7 > n)
                {
                    break;
                }

                uint crc = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(pos, 4));
                ushort length = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(pos + 4, 2));
                byte rtype = data[pos + 6];

                if (crc == 0 && length == 0 && rtype == 0)
                {
                    int skip = Math.Min(blockRem, n - pos);
                    pos += skip;
                    if (!inFragment)
                    {
                        lastValidOffset = pos;
                    }
                    continue;
                }

                if (pos + 7 + length > n)
                {
                    break;
                }

                ReadOnlySpan<byte> chunk = data.Slice(pos + 7, length);
                uint expectedCrc = ComputeMaskedCrc32C(rtype, chunk);
                if (crc != expectedCrc)
                {
                    break;
                }

                pos += 7 + length;

                if (rtype == 1)
                {
                    if (inFragment)
                    {
                        break;
                    }
                    lastValidOffset = pos;
                }
                else if (rtype == 2)
                {
                    if (inFragment)
                    {
                        break;
                    }
                    inFragment = true;
                }
                else if (rtype == 3)
                {
                    if (!inFragment)
                    {
                        break;
                    }
                }
                else if (rtype == 4)
                {
                    if (!inFragment)
                    {
                        break;
                    }
                    inFragment = false;
                    lastValidOffset = pos;
                }
                else
                {
                    break;
                }
            }

            return lastValidOffset;
        }

        private static void ParseLogFile(
            string filePath,
            List<(ulong Seq, byte ValueType, byte[] UserKey, byte[] Value)> output
        )
        {
            byte[] data = File.ReadAllBytes(filePath);
            int pos = 0;
            int n = data.Length;
            using MemoryStream frag = new();
            bool inFragment = false;

            while (pos < n)
            {
                int blockRem = LogBlockSize - (pos % LogBlockSize);
                if (blockRem < 7)
                {
                    if (pos + blockRem > n)
                    {
                        break;
                    }
                    pos += blockRem;
                    continue;
                }

                if (pos + 7 > n)
                {
                    break;
                }

                uint crc = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos, 4));
                ushort length = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(pos + 4, 2));
                byte rtype = data[pos + 6];

                if (crc == 0 && length == 0 && rtype == 0)
                {
                    pos += Math.Min(blockRem, n - pos);
                    continue;
                }

                pos += 7;
                if (pos + length > n)
                {
                    Log.Warning("Truncated LevelDB WAL record in {File} at offset {Offset}. Stopping WAL parse.", filePath, pos - 7);
                    break;
                }

                ReadOnlySpan<byte> chunk = data.AsSpan(pos, length);
                pos += length;

                uint expectedCrc = ComputeMaskedCrc32C(rtype, chunk);
                if (crc != expectedCrc)
                {
                    Log.Warning(
                        "LevelDB WAL CRC32C mismatch in {File} at offset {Offset} (expected 0x{Expected:X8}, got 0x{Actual:X8}). Stopping WAL parse.",
                        filePath,
                        pos - length - 7,
                        expectedCrc,
                        crc
                    );
                    break;
                }

                if (rtype == 1)
                {
                    frag.SetLength(0);
                    frag.Write(chunk);
                    inFragment = false;
                }
                else if (rtype == 2)
                {
                    frag.SetLength(0);
                    frag.Write(chunk);
                    inFragment = true;
                    continue;
                }
                else if (rtype == 3)
                {
                    if (!inFragment)
                    {
                        Log.Warning("Orphaned LevelDB WAL middle fragment in {File}. Stopping WAL parse.", filePath);
                        break;
                    }
                    frag.Write(chunk);
                    continue;
                }
                else if (rtype == 4)
                {
                    if (!inFragment)
                    {
                        Log.Warning("Orphaned LevelDB WAL last fragment in {File}. Stopping WAL parse.", filePath);
                        break;
                    }
                    frag.Write(chunk);
                    inFragment = false;
                }
                else
                {
                    Log.Warning("Unknown LevelDB WAL record type {Type} in {File}. Stopping WAL parse.", rtype, filePath);
                    break;
                }

                byte[] batch = frag.ToArray();
                if (batch.Length < 12)
                {
                    continue;
                }

                ulong baseSeq = BinaryPrimitives.ReadUInt64LittleEndian(batch.AsSpan(0, 8));
                uint count = BinaryPrimitives.ReadUInt32LittleEndian(batch.AsSpan(8, 4));
                int bpos = 12;

                for (uint i = 0; i < count && bpos < batch.Length; i++)
                {
                    byte vtype = batch[bpos++];
                    if (
                        !TryReadVarint(batch, ref bpos, out ulong klenRaw)
                        || klenRaw > (ulong)(batch.Length - bpos)
                    )
                    {
                        break;
                    }

                    int klen = (int)klenRaw;
                    byte[] ukey = batch.AsSpan(bpos, klen).ToArray();
                    bpos += klen;

                    byte[] val = [];
                    if (vtype == 1)
                    {
                        if (
                            !TryReadVarint(batch, ref bpos, out ulong vlenRaw)
                            || vlenRaw > (ulong)(batch.Length - bpos)
                        )
                        {
                            break;
                        }
                        int vlen = (int)vlenRaw;
                        val = batch.AsSpan(bpos, vlen).ToArray();
                        bpos += vlen;
                    }

                    output.Add((baseSeq + i, vtype, ukey, val));
                }
            }
        }

        private static byte[] SnappyDecompress(ReadOnlySpan<byte> compressed)
        {
            int pos = 0;
            if (!TryReadVarint(compressed, ref pos, out ulong rawLen))
            {
                throw new InvalidDataException("Invalid Snappy uncompressed length varint.");
            }
            if (rawLen > 64UL * 1024 * 1024)
            {
                throw new InvalidDataException($"Snappy uncompressed length too large: {rawLen}");
            }

            int uncompressedLen = (int)rawLen;
            byte[] output = new byte[uncompressedLen];
            int outPos = 0;

            while (pos < compressed.Length)
            {
                byte tag = compressed[pos++];
                int tagType = tag & 0x03;

                if (tagType == 0)
                {
                    int len = (tag >> 2) + 1;
                    if (len > 60)
                    {
                        int extraBytes = len - 60;
                        if (pos + extraBytes > compressed.Length)
                        {
                            throw new InvalidDataException("Truncated Snappy literal length.");
                        }

                        len = 0;
                        for (int i = 0; i < extraBytes; i++)
                        {
                            len |= compressed[pos++] << (8 * i);
                        }
                        len += 1;
                    }

                    if (len < 0 || pos + len > compressed.Length || outPos + len > output.Length)
                    {
                        throw new InvalidDataException("Snappy literal out of bounds.");
                    }

                    compressed.Slice(pos, len).CopyTo(output.AsSpan(outPos, len));
                    pos += len;
                    outPos += len;
                }
                else
                {
                    int len;
                    int offset;
                    if (tagType == 1)
                    {
                        if (pos >= compressed.Length)
                        {
                            throw new InvalidDataException("Truncated Snappy copy-1.");
                        }
                        len = ((tag >> 2) & 0x07) + 4;
                        offset = ((tag & 0xE0) << 3) | compressed[pos++];
                    }
                    else if (tagType == 2)
                    {
                        if (pos + 2 > compressed.Length)
                        {
                            throw new InvalidDataException("Truncated Snappy copy-2.");
                        }
                        len = (tag >> 2) + 1;
                        offset = compressed[pos] | (compressed[pos + 1] << 8);
                        pos += 2;
                    }
                    else
                    {
                        if (pos + 4 > compressed.Length)
                        {
                            throw new InvalidDataException("Truncated Snappy copy-4.");
                        }
                        len = (tag >> 2) + 1;
                        offset =
                            compressed[pos]
                            | (compressed[pos + 1] << 8)
                            | (compressed[pos + 2] << 16)
                            | (compressed[pos + 3] << 24);
                        pos += 4;
                    }

                    int copySrc = outPos - offset;
                    if (offset <= 0 || copySrc < 0 || outPos + len > output.Length)
                    {
                        throw new InvalidDataException("Snappy copy offset/length out of bounds.");
                    }

                    for (int i = 0; i < len; i++)
                    {
                        output[outPos++] = output[copySrc + i];
                    }
                }
            }

            return outPos != uncompressedLen
                ? throw new InvalidDataException(
                    $"Truncated Snappy stream: expected {uncompressedLen} bytes, decoded {outPos} bytes."
                )
                : output;
        }

        private static bool TryReadVarint(ReadOnlySpan<byte> buffer, ref int pos, out ulong result)
        {
            result = 0;
            int shift = 0;
            for (int i = 0; i < 10; i++)
            {
                if (pos >= buffer.Length)
                {
                    return false;
                }

                byte b = buffer[pos++];
                if (i == 9 && (b > 1))
                {
                    return false;
                }

                result |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    return true;
                }
                shift += 7;
            }

            return false;
        }

        private static void WriteVarint(Stream stream, ulong value)
        {
            while (true)
            {
                byte b = (byte)(value & 0x7F);
                value >>= 7;
                if (value != 0)
                {
                    stream.WriteByte((byte)(b | 0x80));
                }
                else
                {
                    stream.WriteByte(b);
                    break;
                }
            }
        }

        private static bool StartsWith(byte[] data, byte[] prefix)
        {
            if (data.Length < prefix.Length)
            {
                return false;
            }
            for (int i = 0; i < prefix.Length; i++)
            {
                if (data[i] != prefix[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static string? DecodeIdbStringKey(byte[] ukey, int offset)
        {
            int pos = offset;
            if (!TryReadVarint(ukey, ref pos, out ulong charCountRaw) || charCountRaw == 0)
            {
                return null;
            }

            if (charCountRaw > int.MaxValue / 2)
            {
                return null;
            }

            int charCount = (int)charCountRaw;
            int byteLen = charCount * 2;
            return pos + byteLen > ukey.Length ? null : Encoding.BigEndianUnicode.GetString(ukey, pos, byteLen);
        }

        private static byte[] BuildUserKey(byte[] prefix, string key)
        {
            byte[] utf16Be = Encoding.BigEndianUnicode.GetBytes(key);
            using MemoryStream ms = new(prefix.Length + 4 + utf16Be.Length);
            ms.Write(prefix);
            WriteVarint(ms, (ulong)(utf16Be.Length / 2));
            ms.Write(utf16Be);
            return ms.ToArray();
        }

        private static int IndexOfBytes(byte[] haystack, byte[] needle, int startIndex)
        {
            int limit = haystack.Length - needle.Length;
            for (int i = startIndex; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool IsLevelDbLocked(string levelDbDir)
        {
            string lockPath = Path.Combine(levelDbDir, "LOCK");
            if (!File.Exists(lockPath))
            {
                return false;
            }

            try
            {
                using FileStream fs = new(
                    lockPath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None
                );
                return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch
            {
                return true;
            }
        }

        private static string GetDirectorySnapshotKey(string dirPath, string searchPattern = "*")
        {
            try
            {
                StringBuilder sb = new();
                foreach (string file in Directory.GetFiles(dirPath, searchPattern).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    FileInfo fi = new(file);
                    _ = sb.Append(fi.Name)
                        .Append('|')
                        .Append(fi.Length)
                        .Append('|')
                        .Append(fi.LastWriteTimeUtc.Ticks)
                        .Append(';');
                }
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private (string? CorePath, string? GaragePath, string? HudPath) EnsureCacheDataBundlesIndexed(
            string cacheDataDir
        )
        {
            lock (_syncLock)
            {
                string snapshotKey = GetDirectorySnapshotKey(cacheDataDir, "f_*");
                if (
                    string.Equals(
                        _cachedCacheDataDir,
                        cacheDataDir,
                        StringComparison.OrdinalIgnoreCase
                    )
                    && string.Equals(_cachedCacheDataSnapshot, snapshotKey, StringComparison.Ordinal)
                )
                {
                    return (_cachedCoreBundlePath, _cachedGarageBundlePath, _cachedHudBundlePath);
                }

                if (!string.Equals(_cachedCacheDataDir, cacheDataDir, StringComparison.OrdinalIgnoreCase))
                {
                    _bundleIndexCache.Clear();
                }

                string? corePath = null;
                string? garagePath = null;
                string? hudPath = null;

                HashSet<string> seenFiles = new(StringComparer.OrdinalIgnoreCase);

                foreach (string filePath in Directory.EnumerateFiles(cacheDataDir, "f_*"))
                {
                    _ = seenFiles.Add(filePath);
                    FileInfo fi = new(filePath);
                    long len = fi.Length;
                    long ticks = fi.LastWriteTimeUtc.Ticks;

                    bool isCore;
                    bool isGarage;
                    bool isHud;

                    if (
                        _bundleIndexCache.TryGetValue(filePath, out (long Length, long LastWriteTicks, bool IsCore, bool IsGarage, bool IsHud) cached)
                        && cached.Length == len
                        && cached.LastWriteTicks == ticks
                    )
                    {
                        isCore = cached.IsCore;
                        isGarage = cached.IsGarage;
                        isHud = cached.IsHud;
                    }
                    else
                    {
                        if (!TryReadZstdBundle(filePath, out _, out string jsText))
                        {
                            _bundleIndexCache[filePath] = (len, ticks, false, false, false);
                            continue;
                        }

                        isCore = jsText.Contains("TankPaintMaterial", StringComparison.Ordinal);
                        isGarage = jsText.Contains("app-garage-tank-preview", StringComparison.Ordinal);
                        isHud = !isCore
                            && jsText.Contains("app-inventory-panel", StringComparison.Ordinal)
                            && jsText.Contains("cooldownStartTime", StringComparison.Ordinal);
                        _bundleIndexCache[filePath] = (len, ticks, isCore, isGarage, isHud);
                    }

                    if (corePath == null && isCore)
                    {
                        corePath = filePath;
                    }
                    if (garagePath == null && isGarage)
                    {
                        garagePath = filePath;
                    }
                    if (hudPath == null && isHud)
                    {
                        hudPath = filePath;
                    }
                }

                foreach (string stale in _bundleIndexCache.Keys.Where(k => !seenFiles.Contains(k)).ToList())
                {
                    _ = _bundleIndexCache.Remove(stale);
                }

                _cachedCacheDataDir = cacheDataDir;
                _cachedCacheDataSnapshot = snapshotKey;
                _cachedCoreBundlePath = corePath;
                _cachedGarageBundlePath = garagePath;
                _cachedHudBundlePath = hudPath;
                return (corePath, garagePath, hudPath);
            }
        }

        public (bool ShadersEnabled, bool SkinAnimationsEnabled) GetAppliedScriptStatus(
            string cacheRootPath
        )
        {
            if (!TryResolveProTankiRoot(cacheRootPath, out string rootDir))
            {
                return (false, false);
            }

            string cacheDataDir = Path.Combine(rootDir, "Cache", "Cache_Data");
            if (!Directory.Exists(cacheDataDir))
            {
                return (false, false);
            }

            (string? corePath, _, _) = EnsureCacheDataBundlesIndexed(cacheDataDir);
            string coreJs = string.Empty;
            bool coreRead =
                corePath != null
                && TryReadZstdBundle(corePath, out _, out coreJs);
            bool shadersEnabled =
                coreRead
                && coreJs.Contains(ShadersStartMarker, StringComparison.Ordinal);
            bool skinAnimationsEnabled =
                coreRead
                && coreJs.Contains(SkinAnimStartMarker, StringComparison.Ordinal);
            return (shadersEnabled, skinAnimationsEnabled);
        }

        private const string AnimPaintMarker = "__tsAnimPaintV2";
        private static readonly byte[] ZstdMagic = [0x28, 0xB5, 0x2F, 0xFD];
        [GeneratedRegex(
            @"function\s+[A-Za-z0-9_$]+\((?<arg>[A-Za-z0-9_$]+)\)\{let\s+[A-Za-z0-9_$]+=\k<arg>\['image'\];return\{'width':"
        )]
        private static partial Regex YDollarRegex();

        private const string AnimPaintHelperJs =
            ";window.__tsAnimPaintV2=window.__tsAnimPaintV2||function(tex){try{if(!tex||tex.__tsChecked)return;var img=tex.image;if(!img)return;var w=img.width||0,h=img.height||0;if(w<3||h<2)return;tex.__tsChecked=1;var p=document.createElement('canvas');p.width=3;p.height=1;var px=p.getContext('2d');if(!px)return;px.drawImage(img,0,0,3,1,0,0,3,1);var d=px.getImageData(0,0,3,1).data;if(d[0]!==84||d[1]!==79||d[2]!==83)return;var cols=d[4]||1,rows=d[5]||1,fps=d[6]||24,nf=((d[8]<<8)|d[9])||(cols*rows);var fw=Math.floor(w/cols),fh=Math.floor(h/rows);if(fw<1||fh<1)return;var c=document.createElement('canvas');c.width=fw;c.height=fh;var ctx=c.getContext('2d');if(!ctx)return;function drawFrame(f){var col=f%cols,row=Math.floor(f/cols);ctx.clearRect(0,0,fw,fh);ctx.drawImage(img,col*fw,row*fh,fw,fh,0,0,fw,fh);if(f===0&&fh>1&&fw>=3){ctx.drawImage(img,0,1,3,1,0,0,3,1);}tex.needsUpdate=true;if(tex.source)tex.source.needsUpdate=true;}tex.image=c;if(tex.source)tex.source.data=c;drawFrame(0);tex.__tsCanvas=c;tex.__tsLast=0;var list=window.__tsAnimList||(window.__tsAnimList=[]);list.push({tex:tex,fps:fps,nf:nf,draw:drawFrame,canvas:c});if(!window.__tsAnimLoop){window.__tsAnimLoop=1;var tick=function(){var now=performance.now()*0.001,upd=0;for(var i=list.length-1;i>=0;i--){var it=list[i];if(!it.tex||it.tex.image!==it.canvas){list.splice(i,1);continue;}var f=Math.floor(now*it.fps)%it.nf;if(f!==it.tex.__tsLast){it.tex.__tsLast=f;it.draw(f);upd=1;}}if(upd&&window.__tsGarageStage){try{var gs=window.__tsGarageStage;if(gs.renderer&&gs.renderer.domElement&&gs.renderer.domElement.isConnected&&gs.scene&&gs.camera)gs.renderer.render(gs.scene,gs.camera);}catch(e){}}requestAnimationFrame(tick);};requestAnimationFrame(tick);}}catch(e){}};";

        public bool EnsureAnimatedPaintEnginePatched(string cacheRootPath)
        {
            if (!TryResolveProTankiRoot(cacheRootPath, out string rootDir))
            {
                return false;
            }

            string cacheDataDir = Path.Combine(rootDir, "Cache", "Cache_Data");
            if (!Directory.Exists(cacheDataDir))
            {
                Log.Warning("HTML5 Cache_Data directory not found at {Dir}", cacheDataDir);
                return false;
            }

            string originalsCacheDataDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Backups",
                "Html5",
                "Originals",
                "Cache_Data"
            );

            bool anyCorePatchedOrReady = false;
            bool anyModified = false;

            foreach (string filePath in Directory.EnumerateFiles(cacheDataDir, "f_*"))
            {
                string fileName = Path.GetFileName(filePath);
                if (fileName.Contains('.'))
                {
                    continue;
                }

                byte[] rawBytes;
                try
                {
                    using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (fs.Length < 4)
                    {
                        continue;
                    }

                    byte[] magic = new byte[4];
                    if (fs.Read(magic, 0, 4) != 4 || !magic.AsSpan().SequenceEqual(ZstdMagic))
                    {
                        continue;
                    }

                    fs.Position = 0;
                    rawBytes = new byte[fs.Length];
                    fs.ReadExactly(rawBytes);
                }
                catch
                {
                    continue;
                }

                byte[] decompressed;
                try
                {
                    decompressed = DecompressZstdStream(rawBytes);
                }
                catch
                {
                    continue;
                }

                string jsText = Encoding.UTF8.GetString(decompressed);
                bool isCore = jsText.Contains("TankPaintMaterial", StringComparison.Ordinal);
                bool isGarage = jsText.Contains("app-garage-tank-preview", StringComparison.Ordinal);

                if (!isCore && !isGarage)
                {
                    continue;
                }

                if (isCore)
                {
                    if (jsText.Contains(AnimPaintMarker, StringComparison.Ordinal))
                    {
                        anyCorePatchedOrReady = true;
                    }
                }

                string backupFile = Path.Combine(originalsCacheDataDir, fileName);
                bool hadShaders = jsText.Contains(ShadersStartMarker, StringComparison.Ordinal);
                string? existingShaderBlock = null;
                if (hadShaders)
                {
                    jsText = StripShadersBlock(jsText, out existingShaderBlock);
                }

                bool hasOldOrCurrentPatch =
                    jsText.Contains("__tsAnimPaint", StringComparison.Ordinal)
                    || jsText.Contains("f0=0x64,b0=0x64", StringComparison.Ordinal)
                    || jsText.Contains("__tsGarageStage", StringComparison.Ordinal);

                if (hasOldOrCurrentPatch && File.Exists(backupFile))
                {
                    bool coreUpToDate = !isCore || jsText.Contains(AnimPaintMarker, StringComparison.Ordinal);
                    bool garageUpToDate = !isGarage || (
                        !jsText.Contains("f0=0x2710,b0=0x64", StringComparison.Ordinal)
                        && jsText.Contains("__tsGarageStage", StringComparison.Ordinal)
                    );
                    if (coreUpToDate && garageUpToDate)
                    {
                        continue;
                    }

                    try
                    {
                        byte[] origBytes = File.ReadAllBytes(backupFile);
                        byte[] origDecompressed = DecompressZstdStream(origBytes);
                        string origJs = Encoding.UTF8.GetString(origDecompressed);
                        if (
                            (isCore && origJs.Contains("TankPaintMaterial", StringComparison.Ordinal))
                            || (isGarage && origJs.Contains("app-garage-tank-preview", StringComparison.Ordinal))
                        )
                        {
                            jsText = origJs;
                        }
                    }
                    catch { }
                }

                string patchedJs = jsText;
                bool fileNeedsWrite = false;

                if (isCore && !patchedJs.Contains(AnimPaintMarker, StringComparison.Ordinal))
                {
                    Match m = YDollarRegex().Match(patchedJs);
                    if (m.Success)
                    {
                        string argName = m.Groups["arg"].Value;
                        string hookCall = $"{argName}&&window.{AnimPaintMarker}&&window.{AnimPaintMarker}({argName});";
                        int bracePos = m.Value.IndexOf('{');
                        string replacement =
                            AnimPaintHelperJs
                            + m.Value[..(bracePos + 1)]
                            + hookCall
                            + m.Value[(bracePos + 1)..];

                        patchedJs = string.Concat(
                            patchedJs.AsSpan(0, m.Index),
                            replacement,
                            patchedJs.AsSpan(m.Index + m.Length)
                        );
                        fileNeedsWrite = true;
                    }
                    else
                    {
                        Log.Warning("Could not locate Y$ material helper in HTML5 core bundle {File}", fileName);
                    }
                }

                if (isGarage)
                {
                    if (patchedJs.Contains("f0=0x2710,b0=0x64", StringComparison.Ordinal))
                    {
                        patchedJs = patchedJs.Replace("f0=0x2710,b0=0x64", "f0=0x64,b0=0x64");
                        fileNeedsWrite = true;
                    }

                    const string renderTarget =
                        "['render'](){this['$p562'](),this['renderer']['render'](this['scene'],this['camera']);}";
                    const string renderReplacement =
                        "['render'](){window.__tsGarageStage=this;this['$p562'](),this['renderer']['render'](this['scene'],this['camera']);}";
                    if (
                        !patchedJs.Contains("__tsGarageStage", StringComparison.Ordinal)
                        && patchedJs.Contains(renderTarget, StringComparison.Ordinal)
                    )
                    {
                        patchedJs = patchedJs.Replace(renderTarget, renderReplacement);
                        fileNeedsWrite = true;
                    }
                }

                if (!fileNeedsWrite)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(existingShaderBlock))
                {
                    patchedJs = existingShaderBlock + patchedJs;
                }

                _ = Directory.CreateDirectory(originalsCacheDataDir);
                if (!File.Exists(backupFile))
                {
                    byte[] cleanBackupBytes = hadShaders
                        ? CompressZstdRaw(Encoding.UTF8.GetBytes(jsText))
                        : rawBytes;
                    File.WriteAllBytes(backupFile, cleanBackupBytes);
                }

                string data1Path = Path.Combine(cacheDataDir, "data_1");
                string data1Backup = Path.Combine(originalsCacheDataDir, "data_1");
                if (File.Exists(data1Path) && !File.Exists(data1Backup))
                {
                    File.Copy(data1Path, data1Backup, overwrite: false);
                }

                byte[] patchedUtf8 = Encoding.UTF8.GetBytes(patchedJs);
                byte[] recompressed = CompressZstdForCache(patchedUtf8, rawBytes.Length, out bool exactSizeMatched);

                if (!exactSizeMatched && File.Exists(data1Path))
                {
                    UpdateData1FileSize(data1Path, fileName, recompressed.Length);
                }

                File.WriteAllBytes(filePath, recompressed);
                if (isCore)
                {
                    if (
                        TryReadZstdBundle(filePath, out _, out string verifyJs)
                        && verifyJs.Contains(AnimPaintMarker, StringComparison.Ordinal)
                    )
                    {
                        anyCorePatchedOrReady = true;
                    }
                }
                anyModified = true;
                Log.Information("Patched HTML5 cached JS bundle {File} for Animated Paints.", fileName);
            }

            if (anyModified)
            {
                ClearCodeCacheJs(rootDir);
            }

            return anyCorePatchedOrReady;
        }

        public bool RestoreAnimatedPaintEngine(string cacheRootPath)
        {
            if (!TryResolveProTankiRoot(cacheRootPath, out string rootDir))
            {
                return false;
            }

            string originalsCacheDataDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Backups",
                "Html5",
                "Originals",
                "Cache_Data"
            );
            if (!Directory.Exists(originalsCacheDataDir))
            {
                return false;
            }

            string cacheDataDir = Path.Combine(rootDir, "Cache", "Cache_Data");
            if (!Directory.Exists(cacheDataDir))
            {
                return false;
            }

            (string? corePath, string? garagePath, _) = EnsureCacheDataBundlesIndexed(cacheDataDir);
            string?[] targetBundles = [corePath, garagePath];

            bool restoredAny = false;
            foreach (string? destPath in targetBundles)
            {
                if (string.IsNullOrEmpty(destPath) || !File.Exists(destPath))
                {
                    continue;
                }

                string fileName = Path.GetFileName(destPath);
                string backupFile = Path.Combine(originalsCacheDataDir, fileName);
                if (!File.Exists(backupFile))
                {
                    continue;
                }

                if (!TryReadZstdBundle(destPath, out byte[] currentBytes, out string currentJs))
                {
                    continue;
                }

                bool hasAnimOrGaragePatch =
                    currentJs.Contains("__tsAnimPaint", StringComparison.Ordinal)
                    || currentJs.Contains("f0=0x64,b0=0x64", StringComparison.Ordinal)
                    || currentJs.Contains("__tsGarageStage", StringComparison.Ordinal);
                if (!hasAnimOrGaragePatch)
                {
                    continue;
                }

                if (
                    currentJs.Contains(ShadersStartMarker, StringComparison.Ordinal)
                    && TryReadZstdBundle(backupFile, out _, out string backupJs)
                )
                {
                    _ = StripShadersBlock(currentJs, out string? activeShaderBlock);
                    if (!string.IsNullOrEmpty(activeShaderBlock))
                    {
                        WritePatchedZstdBundle(
                            cacheDataDir,
                            originalsCacheDataDir,
                            destPath,
                            fileName,
                            currentBytes,
                            currentBytes.Length,
                            activeShaderBlock + StripShadersBlock(backupJs, out _)
                        );
                        restoredAny = true;
                        continue;
                    }
                }

                if (TryReadZstdBundle(backupFile, out byte[] origBytes, out _))
                {
                    string data1Path = Path.Combine(cacheDataDir, "data_1");
                    if (origBytes.Length != currentBytes.Length && File.Exists(data1Path))
                    {
                        UpdateData1FileSize(data1Path, fileName, origBytes.Length);
                    }
                    File.Copy(backupFile, destPath, overwrite: true);
                    restoredAny = true;
                }
            }

            if (restoredAny)
            {
                ClearCodeCacheJs(rootDir);
                InvalidateCache();
                Log.Information("Restored original HTML5 Cache_Data JS bundles for Animated Paints.");
            }

            return restoredAny;
        }

        private static string ComputeShortHash(string content)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
            return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
        }

        private const string ShadersStartMarker = "/*__TS_SHADERS_START_V1__*/";
        private const string ShadersEndMarker = "/*__TS_SHADERS_END_V1__*/";
        private const string ShadersHashMarkerPrefix = "/*__TS_SHADERS_HASH:";
        private const string ShadersHashMarkerSuffix = "__*/";

        private const string SkinAnimStartMarker = "/*__TS_SKIN_ANIM_START_V1__*/";
        private const string SkinAnimEndMarker = "/*__TS_SKIN_ANIM_END_V1__*/";
        private const string RepairCooldownMarker = "/*__TS_REPAIR_COOLDOWN_V3__*/";
        private const string LegacyRepairCooldownMarkerV2 = "/*__TS_REPAIR_COOLDOWN_V2__*/";
        private const string LegacyRepairCooldownMarkerV1 = "/*__TS_REPAIR_COOLDOWN_V1__*/";
        private const string SkinAnimHashMarkerPrefix = "/*__TS_SKIN_ANIM_HASH:";
        private const string SkinAnimHashMarkerSuffix = "__*/";

        public bool IsShadersEnabled(string cacheRootPath)
        {
            return GetAppliedScriptStatus(cacheRootPath).ShadersEnabled;
        }

        public (bool Success, string? ErrorMessage) ApplyShaders(
            string cacheRootPath,
            string toggleHotkey = "F8"
        )
        {
            if (!TryResolveProTankiRoot(cacheRootPath, out string rootDir))
            {
                return (false, "HTML5 cache root directory not found.");
            }

            string cacheDataDir = Path.Combine(rootDir, "Cache", "Cache_Data");
            if (!Directory.Exists(cacheDataDir))
            {
                return (false, "HTML5 Cache_Data directory not found.");
            }

            string? shaderBlock = LoadShaderScriptPayload(toggleHotkey);
            if (string.IsNullOrEmpty(shaderBlock))
            {
                return (false, "ProTanki Pack script asset (Assets/Pack/protanki_pack.js) not found.");
            }

            string originalsCacheDataDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Backups",
                "Html5",
                "Originals",
                "Cache_Data"
            );

            bool foundCoreBundle = false;
            bool anyModified = false;

            foreach (string filePath in Directory.EnumerateFiles(cacheDataDir, "f_*"))
            {
                string fileName = Path.GetFileName(filePath);
                if (!TryReadZstdBundle(filePath, out byte[] rawBytes, out string jsText))
                {
                    continue;
                }

                if (!jsText.Contains("TankPaintMaterial", StringComparison.Ordinal))
                {
                    continue;
                }

                foundCoreBundle = true;
                string cleanJs = StripShadersBlock(jsText, out string? existingBlock);
                bool hadLegacySkinAnim = cleanJs.Contains(SkinAnimStartMarker, StringComparison.Ordinal);
                if (hadLegacySkinAnim)
                {
                    cleanJs = StripSkinAnimBlock(cleanJs, out _);
                }
                if (
                    !hadLegacySkinAnim
                    && string.Equals(existingBlock, shaderBlock, StringComparison.Ordinal)
                )
                {
                    continue;
                }

                byte[] backupRawBytes = rawBytes;
                string backupFile = Path.Combine(originalsCacheDataDir, fileName);
                if (!File.Exists(backupFile) && (!string.IsNullOrEmpty(existingBlock) || hadLegacySkinAnim))
                {
                    backupRawBytes = CompressZstdRaw(Encoding.UTF8.GetBytes(cleanJs));
                }

                string patchedJs = shaderBlock + cleanJs;
                WritePatchedZstdBundle(
                    cacheDataDir,
                    originalsCacheDataDir,
                    filePath,
                    fileName,
                    backupRawBytes,
                    rawBytes.Length,
                    patchedJs,
                    saveBackup: !cleanJs.Contains("__tsAnimPaint", StringComparison.Ordinal)
                );
                anyModified = true;
                Log.Information(
                    "Applied ProTanki Pack to cached bundle {File}.",
                    fileName
                );
            }

            if (anyModified)
            {
                ClearCodeCacheJs(rootDir);
            }

            if (!foundCoreBundle)
            {
                return (
                    false,
                    "Core 3D engine bundle not found in cache yet. Please launch ProTanki once to cache the game files."
                );
            }

            SyncRepairCooldown(cacheRootPath, enabled: true);

            return (true, null);
        }

        public (bool Success, string? ErrorMessage) RestoreDefaultShaders(string cacheRootPath)
        {
            if (!TryResolveProTankiRoot(cacheRootPath, out string rootDir))
            {
                return (true, null);
            }

            string cacheDataDir = Path.Combine(rootDir, "Cache", "Cache_Data");
            if (!Directory.Exists(cacheDataDir))
            {
                return (true, null);
            }

            string originalsCacheDataDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Backups",
                "Html5",
                "Originals",
                "Cache_Data"
            );

            bool anyModified = false;

            foreach (string filePath in Directory.EnumerateFiles(cacheDataDir, "f_*"))
            {
                string fileName = Path.GetFileName(filePath);
                if (!TryReadZstdBundle(filePath, out byte[] rawBytes, out string jsText))
                {
                    continue;
                }

                if (!jsText.Contains(ShadersStartMarker, StringComparison.Ordinal))
                {
                    continue;
                }

                string strippedJs = StripShadersBlock(jsText, out _);
                bool hasAnimPaint = strippedJs.Contains("__tsAnimPaint", StringComparison.Ordinal);
                bool hasSkinAnim = strippedJs.Contains(SkinAnimStartMarker, StringComparison.Ordinal);

                string backupFile = Path.Combine(originalsCacheDataDir, fileName);
                bool restoredFromBackup = false;

                if (!hasAnimPaint && !hasSkinAnim && File.Exists(backupFile))
                {
                    try
                    {
                        if (
                            TryReadZstdBundle(backupFile, out byte[] backupBytes, out string backupJs)
                            && backupJs.Contains("TankPaintMaterial", StringComparison.Ordinal)
                            && !backupJs.Contains(ShadersStartMarker, StringComparison.Ordinal)
                        )
                        {
                            string data1Path = Path.Combine(cacheDataDir, "data_1");
                            if (backupBytes.Length != rawBytes.Length && File.Exists(data1Path))
                            {
                                UpdateData1FileSize(data1Path, fileName, backupBytes.Length);
                            }

                            File.Copy(backupFile, filePath, overwrite: true);
                            restoredFromBackup = true;
                        }
                    }
                    catch { }
                }

                if (!restoredFromBackup)
                {
                    WritePatchedZstdBundle(
                        cacheDataDir,
                        originalsCacheDataDir,
                        filePath,
                        fileName,
                        rawBytes,
                        rawBytes.Length,
                        strippedJs
                    );
                }

                anyModified = true;
                Log.Information("Removed ProTanki Pack from cached bundle {File}.", fileName);
            }

            if (anyModified)
            {
                ClearCodeCacheJs(rootDir);
            }

            SyncRepairCooldown(cacheRootPath, enabled: false);

            return (true, null);
        }

        private static string StripShadersBlock(string jsText, out string? extractedBlock)
        {
            return StripMarkerBlock(jsText, ShadersStartMarker, ShadersEndMarker, out extractedBlock);
        }

        private static string StripSkinAnimBlock(string jsText, out string? extractedBlock)
        {
            return StripMarkerBlock(jsText, SkinAnimStartMarker, SkinAnimEndMarker, out extractedBlock);
        }

        private void SyncRepairCooldown(string cacheRootPath, bool enabled)
        {
            if (!TryResolveProTankiRoot(cacheRootPath, out string rootDir))
            {
                return;
            }

            string cacheDataDir = Path.Combine(rootDir, "Cache", "Cache_Data");
            if (!Directory.Exists(cacheDataDir))
            {
                return;
            }

            _ = EnsureCacheDataBundlesIndexed(cacheDataDir);
            List<string> hudPaths = [.. _bundleIndexCache
                .Where(kv => kv.Value.IsHud && File.Exists(kv.Key))
                .Select(kv => kv.Key)];
            if (hudPaths.Count == 0)
            {
                return;
            }

            string originalsCacheDataDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Backups",
                "Html5",
                "Originals",
                "Cache_Data"
            );
            bool anyModified = false;

            foreach (string hudPath in hudPaths)
            {
                if (!TryReadZstdBundle(hudPath, out byte[] rawBytes, out string jsText))
                {
                    continue;
                }

                bool hasCurrentPatch = jsText.Contains(RepairCooldownMarker, StringComparison.Ordinal);
                bool hasLegacyPatch =
                    jsText.Contains(LegacyRepairCooldownMarkerV2, StringComparison.Ordinal)
                    || jsText.Contains(LegacyRepairCooldownMarkerV1, StringComparison.Ordinal);
                if (enabled && hasCurrentPatch)
                {
                    continue;
                }
                if (!enabled && !hasCurrentPatch && !hasLegacyPatch)
                {
                    continue;
                }

                bool migrated = false;
                string sourceJs = hasLegacyPatch
                    ? RestoreLegacyRepairCooldownPatch(jsText, out migrated)
                    : jsText;
                if (hasLegacyPatch && !migrated)
                {
                    Log.Warning("Could not migrate legacy repair cooldown patch in HTML5 HUD bundle {File}", Path.GetFileName(hudPath));
                    continue;
                }

                bool applied = false;
                string patchedJs = enabled
                    ? ApplyRepairCooldownPatch(sourceJs, out applied)
                    : hasCurrentPatch
                        ? RemoveRepairCooldownPatch(sourceJs, out applied)
                        : sourceJs;
                if (!enabled && hasLegacyPatch)
                {
                    applied = true;
                }
                if (!applied)
                {
                    Log.Warning(
                        "Could not safely {Action} repair cooldown patch in HTML5 HUD bundle {File}",
                        enabled ? "apply" : "remove",
                        Path.GetFileName(hudPath)
                    );
                    continue;
                }
                if (string.Equals(jsText, patchedJs, StringComparison.Ordinal))
                {
                    continue;
                }

                string fileName = Path.GetFileName(hudPath);
                WritePatchedZstdBundle(
                    cacheDataDir,
                    originalsCacheDataDir,
                    hudPath,
                    fileName,
                    rawBytes,
                    rawBytes.Length,
                    patchedJs
                );
                anyModified = true;
                Log.Information(
                    "{Action} repair cooldown persistence in HTML5 HUD bundle {File}.",
                    enabled ? "Applied" : "Removed",
                    fileName
                );
            }

            if (anyModified)
            {
                ClearCodeCacheJs(rootDir);
                InvalidateCache();
            }
        }

        private static readonly Regex RepairLockedGuardRegex = new(
            @"if\((?<mf>[A-Za-z0-9_$]+)\((?<slot>[A-Za-z0-9_$]+)\)\)return (?<assign>[A-Za-z0-9_$]+)\((?<merge>[A-Za-z0-9_$]+)\(\k<merge>\(\{\},\k<slot>\),(?<lI>[A-Za-z0-9_$]+)\)",
            RegexOptions.Compiled
        );

        private static readonly Regex RepairFullHealthStateRegex = new(
            @"return (?<full>[A-Za-z0-9_$]+)\?(?<merge>[A-Za-z0-9_$]+)\(\k<merge>\(\{\},(?<slot>[A-Za-z0-9_$]+)\),(?<lI>[A-Za-z0-9_$]+)\):(?<mf>[A-Za-z0-9_$]+)\(\k<slot>\)\|\|\(\k<slot>\['item'\]\?\.\['cooldownStartTime'\]\?\?-0x1\)>=0x0\?\k<slot>:\k<merge>\(\k<merge>\(\{\},\k<slot>\),(?<qd>[A-Za-z0-9_$]+)\);",
            RegexOptions.Compiled
        );

        private static readonly Regex PatchedRepairLockedGuardRegex = new(
            @"if\((?<mf>[A-Za-z0-9_$]+)\((?<slot>[A-Za-z0-9_$]+)\)&&!\(\k<slot>\['item'\]\['id'\]===0x1&&\(\k<slot>\['lockBits'\]&0x2\)!==0&&\(\k<slot>\['lockBits'\]&0x1\)===0(?:&&\(\k<slot>\['item'\]\['cooldownStartTime'\]\?\?-0x1\)>=0x0)?\)\)return (?<assign>[A-Za-z0-9_$]+)\((?<merge>[A-Za-z0-9_$]+)\(\k<merge>\(\{\},\k<slot>\),(?<lI>[A-Za-z0-9_$]+)\)",
            RegexOptions.Compiled
        );

        private static readonly Regex PatchedRepairFullHealthStateRegex = new(
            @"return (?<full>[A-Za-z0-9_$]+)\?\((?<slot>[A-Za-z0-9_$]+)\['item'\]\?\.\['cooldownStartTime'\]\?\?-0x1\)>=0x0&&\k<slot>\['item'\]\['cooldownTime'\]>0x0&&\(performance\['now'\]\(\)-\k<slot>\['item'\]\['cooldownStartTime'\]\)<\k<slot>\['item'\]\['cooldownTime'\]\?(?<merge>[A-Za-z0-9_$]+)\(\k<merge>\(\{\},\k<slot>\),\{'cooldownVisible':!!\[\],'cooldownStatus':Math\['max'\]\(0x0,Math\['min'\]\(0x1,\(performance\['now'\]\(\)-\k<slot>\['item'\]\['cooldownStartTime'\]\)/\k<slot>\['item'\]\['cooldownTime'\]\)\)\}\):\k<merge>\(\k<merge>\(\{\},\k<slot>\),(?<lI>[A-Za-z0-9_$]+)\):(?<mf>[A-Za-z0-9_$]+)\(\k<slot>\)\|\|\(\k<slot>\['item'\]\?\.\['cooldownStartTime'\]\?\?-0x1\)>=0x0\?\k<slot>:\k<merge>\(\k<merge>\(\{\},\k<slot>\),(?<qd>[A-Za-z0-9_$]+)\);",
            RegexOptions.Compiled
        );

        private static readonly Regex LegacyRepairFullHealthStateRegex = new(
            @"return (?<full>[A-Za-z0-9_$]+)\?(?<merge>[A-Za-z0-9_$]+)\(\k<merge>\(\{\},(?<slot>[A-Za-z0-9_$]+)\),.*?\):(?<mf>[A-Za-z0-9_$]+)\(\k<slot>\)\|\|\(\k<slot>\['item'\]\?\.\['cooldownStartTime'\]\?\?-0x1\)>=0x0\?\k<slot>:\k<merge>\(\k<merge>\(\{\},\k<slot>\),(?<qd>[A-Za-z0-9_$]+)\);",
            RegexOptions.Compiled
        );

        private static readonly Regex LegacyRepairFullHealthStateV2Regex = new(
            @"return (?<full>[A-Za-z0-9_$]+)\?(?<slot>[A-Za-z0-9_$]+)\['item'\]\['cooldownStartTime'\]>=0&&\k<slot>\['item'\]\['cooldownTime'\]>0&&[^?]+\?(?<merge>[A-Za-z0-9_$]+)\(\k<merge>\(\{\},[A-Za-z0-9_$]+\(\k<slot>,[^)]+\)\),\{'lockBits':\k<slot>\['lockBits'\]\|0x2\}\):\k<merge>\(\k<merge>\(\{\},\k<slot>\),(?<lI>[A-Za-z0-9_$]+)\):(?<mf>[A-Za-z0-9_$]+)\(\k<slot>\)\|\|\(\k<slot>\['item'\]\?\.\['cooldownStartTime'\]\?\?-0x1\)>=0x0\?\k<slot>:\k<merge>\(\k<merge>\(\{\},\k<slot>\),(?<qd>[A-Za-z0-9_$]+)\);",
            RegexOptions.Compiled
        );

        private static string RestoreLegacyRepairCooldownPatch(string jsText, out bool restored)
        {
            restored = false;
            string? marker = jsText.StartsWith(LegacyRepairCooldownMarkerV2, StringComparison.Ordinal)
                ? LegacyRepairCooldownMarkerV2
                : jsText.StartsWith(LegacyRepairCooldownMarkerV1, StringComparison.Ordinal)
                    ? LegacyRepairCooldownMarkerV1
                    : null;
            if (marker == null)
            {
                return jsText;
            }

            string body = jsText[marker.Length..];
            Match guardMatch = PatchedRepairLockedGuardRegex.Match(body);
            if (!guardMatch.Success || PatchedRepairLockedGuardRegex.Matches(body).Count != 1)
            {
                return jsText;
            }

            body = PatchedRepairLockedGuardRegex.Replace(
                body,
                m => $"if({m.Groups["mf"].Value}({m.Groups["slot"].Value}))return {m.Groups["assign"].Value}({m.Groups["merge"].Value}({m.Groups["merge"].Value}({{}},{m.Groups["slot"].Value}),{m.Groups["lI"].Value})",
                1
            );

            if (LegacyRepairFullHealthStateV2Regex.Matches(body).Count == 1)
            {
                body = LegacyRepairFullHealthStateV2Regex.Replace(
                    body,
                    m => $"return {m.Groups["full"].Value}?{m.Groups["merge"].Value}({m.Groups["merge"].Value}({{}},{m.Groups["slot"].Value}),{m.Groups["lI"].Value}):{m.Groups["mf"].Value}({m.Groups["slot"].Value})||({m.Groups["slot"].Value}['item']?.['cooldownStartTime']??-0x1)>=0x0?{m.Groups["slot"].Value}:{m.Groups["merge"].Value}({m.Groups["merge"].Value}({{}},{m.Groups["slot"].Value}),{m.Groups["qd"].Value});",
                    1
                );
                restored = true;
                return body;
            }

            if (LegacyRepairFullHealthStateRegex.Matches(body).Count == 1)
            {
                const string lIPlaceholder = "lI";
                body = LegacyRepairFullHealthStateRegex.Replace(
                    body,
                    m => $"return {m.Groups["full"].Value}?{m.Groups["merge"].Value}({m.Groups["merge"].Value}({{}},{m.Groups["slot"].Value}),{guardMatch.Groups["lI"].Value ?? lIPlaceholder}):{m.Groups["mf"].Value}({m.Groups["slot"].Value})||({m.Groups["slot"].Value}['item']?.['cooldownStartTime']??-0x1)>=0x0?{m.Groups["slot"].Value}:{m.Groups["merge"].Value}({m.Groups["merge"].Value}({{}},{m.Groups["slot"].Value}),{m.Groups["qd"].Value});",
                    1
                );
                restored = true;
                return body;
            }

            return jsText;
        }

        private static string ApplyRepairCooldownPatch(string jsText, out bool applied)
        {
            applied = false;
            if (jsText.Contains(RepairCooldownMarker, StringComparison.Ordinal))
            {
                return jsText;
            }

            if (!jsText.Contains("app-inventory-panel", StringComparison.Ordinal))
            {
                return jsText;
            }

            MatchCollection guardMatches = RepairLockedGuardRegex.Matches(jsText);
            MatchCollection stateMatches = RepairFullHealthStateRegex.Matches(jsText);
            if (guardMatches.Count != 1 || stateMatches.Count != 1)
            {
                return jsText;
            }

            string patched = RepairLockedGuardRegex.Replace(
                jsText,
                m => $"if({m.Groups["mf"].Value}({m.Groups["slot"].Value})&&!({m.Groups["slot"].Value}['item']['id']===0x1&&({m.Groups["slot"].Value}['lockBits']&0x2)!==0&&({m.Groups["slot"].Value}['lockBits']&0x1)===0&&({m.Groups["slot"].Value}['item']['cooldownStartTime']??-0x1)>=0x0))return {m.Groups["assign"].Value}({m.Groups["merge"].Value}({m.Groups["merge"].Value}({{}},{m.Groups["slot"].Value}),{m.Groups["lI"].Value})",
                1
            );
            patched = RepairFullHealthStateRegex.Replace(
                patched,
                m => $"return {m.Groups["full"].Value}?({m.Groups["slot"].Value}['item']?.['cooldownStartTime']??-0x1)>=0x0&&{m.Groups["slot"].Value}['item']['cooldownTime']>0x0&&(performance['now']()-{m.Groups["slot"].Value}['item']['cooldownStartTime'])<{m.Groups["slot"].Value}['item']['cooldownTime']?{m.Groups["merge"].Value}({m.Groups["merge"].Value}({{}},{m.Groups["slot"].Value}),{{'cooldownVisible':!![],'cooldownStatus':Math['max'](0x0,Math['min'](0x1,(performance['now']()-{m.Groups["slot"].Value}['item']['cooldownStartTime'])/{m.Groups["slot"].Value}['item']['cooldownTime']))}}):{m.Groups["merge"].Value}({m.Groups["merge"].Value}({{}},{m.Groups["slot"].Value}),{m.Groups["lI"].Value}):{m.Groups["mf"].Value}({m.Groups["slot"].Value})||({m.Groups["slot"].Value}['item']?.['cooldownStartTime']??-0x1)>=0x0?{m.Groups["slot"].Value}:{m.Groups["merge"].Value}({m.Groups["merge"].Value}({{}},{m.Groups["slot"].Value}),{m.Groups["qd"].Value});",
                1
            );
            patched = patched.Insert(0, RepairCooldownMarker);
            applied = true;
            return patched;
        }

        private static string RemoveRepairCooldownPatch(string jsText, out bool removed)
        {
            removed = false;
            if (!jsText.StartsWith(RepairCooldownMarker, StringComparison.Ordinal))
            {
                return jsText;
            }

            string body = jsText[RepairCooldownMarker.Length..];
            if (
                PatchedRepairLockedGuardRegex.Matches(body).Count != 1
                || PatchedRepairFullHealthStateRegex.Matches(body).Count != 1
            )
            {
                return jsText;
            }

            string restored = PatchedRepairLockedGuardRegex.Replace(
                body,
                m => $"if({m.Groups["mf"].Value}({m.Groups["slot"].Value}))return {m.Groups["assign"].Value}({m.Groups["merge"].Value}({m.Groups["merge"].Value}({{}},{m.Groups["slot"].Value}),{m.Groups["lI"].Value})",
                1
            );
            restored = PatchedRepairFullHealthStateRegex.Replace(
                restored,
                m => $"return {m.Groups["full"].Value}?{m.Groups["merge"].Value}({m.Groups["merge"].Value}({{}},{m.Groups["slot"].Value}),{m.Groups["lI"].Value}):{m.Groups["mf"].Value}({m.Groups["slot"].Value})||({m.Groups["slot"].Value}['item']?.['cooldownStartTime']??-0x1)>=0x0?{m.Groups["slot"].Value}:{m.Groups["merge"].Value}({m.Groups["merge"].Value}({{}},{m.Groups["slot"].Value}),{m.Groups["qd"].Value});",
                1
            );
            removed = true;
            return restored;
        }

        private static string StripMarkerBlock(
            string jsText,
            string startMarker,
            string endMarker,
            out string? extractedBlock
        )
        {
            extractedBlock = null;
            int startIdx = jsText.IndexOf(startMarker, StringComparison.Ordinal);
            if (startIdx < 0)
            {
                return jsText;
            }

            int endIdx = jsText.IndexOf(endMarker, startIdx, StringComparison.Ordinal);
            if (endIdx < 0)
            {
                return jsText;
            }

            int blockEnd = endIdx + endMarker.Length;
            if (blockEnd < jsText.Length && jsText[blockEnd] == '\r')
            {
                blockEnd++;
            }
            if (blockEnd < jsText.Length && jsText[blockEnd] == '\n')
            {
                blockEnd++;
            }

            extractedBlock = jsText[startIdx..blockEnd];
            return string.Concat(jsText.AsSpan(0, startIdx), jsText.AsSpan(blockEnd));
        }

        public (bool Success, string? ErrorMessage) ApplySkinAnimations(
            string cacheRootPath,
            string toggleHotkey = "F9"
        )
        {
            if (!TryResolveProTankiRoot(cacheRootPath, out string rootDir))
            {
                return (false, "HTML5 cache root directory not found.");
            }

            string cacheDataDir = Path.Combine(rootDir, "Cache", "Cache_Data");
            if (!Directory.Exists(cacheDataDir))
            {
                return (false, "HTML5 Cache_Data directory not found.");
            }

            string? animBlock = LoadSkinAnimScriptPayload(toggleHotkey, out _);
            if (string.IsNullOrEmpty(animBlock))
            {
                return (false, "Skin animations script asset (Assets/Animations/protanki_skin_animations.js) not found.");
            }

            string originalsCacheDataDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Backups",
                "Html5",
                "Originals",
                "Cache_Data"
            );

            bool foundCoreBundle = false;
            bool anyModified = false;

            foreach (string filePath in Directory.EnumerateFiles(cacheDataDir, "f_*"))
            {
                string fileName = Path.GetFileName(filePath);
                if (!TryReadZstdBundle(filePath, out byte[] rawBytes, out string jsText))
                {
                    continue;
                }

                if (!jsText.Contains("TankPaintMaterial", StringComparison.Ordinal))
                {
                    continue;
                }

                foundCoreBundle = true;
                string cleanJs = StripSkinAnimBlock(jsText, out string? existingBlock);
                if (string.Equals(existingBlock, animBlock, StringComparison.Ordinal))
                {
                    continue;
                }

                byte[] backupRawBytes = rawBytes;
                string backupFile = Path.Combine(originalsCacheDataDir, fileName);
                if (!File.Exists(backupFile) && !string.IsNullOrEmpty(existingBlock))
                {
                    backupRawBytes = CompressZstdRaw(Encoding.UTF8.GetBytes(cleanJs));
                }

                string patchedJs = animBlock + cleanJs;
                WritePatchedZstdBundle(
                    cacheDataDir,
                    originalsCacheDataDir,
                    filePath,
                    fileName,
                    backupRawBytes,
                    rawBytes.Length,
                    patchedJs,
                    saveBackup: !cleanJs.Contains("__tsAnimPaint", StringComparison.Ordinal)
                        && !cleanJs.Contains(ShadersStartMarker, StringComparison.Ordinal)
                );
                anyModified = true;
                Log.Information(
                    "Applied ProTanki Pack to cached bundle {File}.",
                    fileName
                );
            }

            if (anyModified)
            {
                ClearCodeCacheJs(rootDir);
            }

            if (!foundCoreBundle)
            {
                return (
                    false,
                    "Core 3D engine bundle not found in cache yet. Please launch ProTanki once to cache the game files."
                );
            }

            return (true, null);
        }

        public (bool Success, string? ErrorMessage) RestoreDefaultSkinAnimations(string cacheRootPath)
        {
            if (!TryResolveProTankiRoot(cacheRootPath, out string rootDir))
            {
                return (true, null);
            }

            string cacheDataDir = Path.Combine(rootDir, "Cache", "Cache_Data");
            if (!Directory.Exists(cacheDataDir))
            {
                return (true, null);
            }

            string originalsCacheDataDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Backups",
                "Html5",
                "Originals",
                "Cache_Data"
            );

            bool anyModified = false;

            foreach (string filePath in Directory.EnumerateFiles(cacheDataDir, "f_*"))
            {
                string fileName = Path.GetFileName(filePath);
                if (!TryReadZstdBundle(filePath, out byte[] rawBytes, out string jsText))
                {
                    continue;
                }

                if (!jsText.Contains(SkinAnimStartMarker, StringComparison.Ordinal))
                {
                    continue;
                }

                string strippedJs = StripSkinAnimBlock(jsText, out _);
                bool hasAnimPaint = strippedJs.Contains("__tsAnimPaint", StringComparison.Ordinal);
                bool hasShaders = strippedJs.Contains(ShadersStartMarker, StringComparison.Ordinal);

                string backupFile = Path.Combine(originalsCacheDataDir, fileName);
                bool restoredFromBackup = false;

                if (!hasAnimPaint && !hasShaders && File.Exists(backupFile))
                {
                    try
                    {
                        if (
                            TryReadZstdBundle(backupFile, out byte[] backupBytes, out string backupJs)
                            && backupJs.Contains("TankPaintMaterial", StringComparison.Ordinal)
                            && !backupJs.Contains(SkinAnimStartMarker, StringComparison.Ordinal)
                        )
                        {
                            string data1Path = Path.Combine(cacheDataDir, "data_1");
                            if (backupBytes.Length != rawBytes.Length && File.Exists(data1Path))
                            {
                                UpdateData1FileSize(data1Path, fileName, backupBytes.Length);
                            }

                            File.Copy(backupFile, filePath, overwrite: true);
                            restoredFromBackup = true;
                        }
                    }
                    catch { }
                }

                if (!restoredFromBackup)
                {
                    WritePatchedZstdBundle(
                        cacheDataDir,
                        originalsCacheDataDir,
                        filePath,
                        fileName,
                        rawBytes,
                        rawBytes.Length,
                        strippedJs
                    );
                }

                anyModified = true;
                Log.Information("Removed HTML5 Skin Animations from cached bundle {File}.", fileName);
            }

            if (anyModified)
            {
                ClearCodeCacheJs(rootDir);
            }

            return (true, null);
        }

        private static string? LoadSkinAnimScriptPayload(string toggleHotkey, out string hashMarker)
        {
            hashMarker = string.Empty;
            string scriptPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets",
                "Animations",
                "protanki_skin_animations.js"
            );
            if (!File.Exists(scriptPath))
            {
                return null;
            }

            string cleanKey = Core.Constants.NormalizeShaderHotkey(toggleHotkey);
            string encodedKey = JsonSerializer.Serialize(cleanKey);
            string safeKeyToken = encodedKey.Trim('"');

            string script = File.ReadAllText(scriptPath, Encoding.UTF8)
                .Replace("__TS_SKIN_ANIM_HOTKEY__", safeKeyToken, StringComparison.Ordinal);

            hashMarker = $"{SkinAnimHashMarkerPrefix}{ComputeShortHash(script)}{SkinAnimHashMarkerSuffix}";
            return $"{SkinAnimStartMarker}\n{hashMarker}\n{script}\n{SkinAnimEndMarker}\n";
        }

        public bool IsShadersUpdatePending(string cacheRootPath, string shaderHotkey)
        {
            if (!TryResolveProTankiRoot(cacheRootPath, out string rootDir))
            {
                return false;
            }

            string cacheDataDir = Path.Combine(rootDir, "Cache", "Cache_Data");
            if (!Directory.Exists(cacheDataDir))
            {
                return false;
            }

            _ = LoadShaderScriptPayload(shaderHotkey, out string expectedShadersHash);
            if (string.IsNullOrEmpty(expectedShadersHash))
            {
                return false;
            }

            (string? corePath, _, _) = EnsureCacheDataBundlesIndexed(cacheDataDir);
            if (corePath == null || !TryReadZstdBundle(corePath, out _, out string coreJs))
            {
                return false;
            }

            bool hasShaders = coreJs.Contains(ShadersStartMarker, StringComparison.Ordinal);
            return hasShaders && !coreJs.Contains(expectedShadersHash, StringComparison.Ordinal);
        }

        public void SyncAppliedScripts(
            string cacheRootPath,
            bool shadersEnabled,
            string shaderHotkey,
            bool skinAnimEnabled = false,
            string skinAnimHotkey = "F9"
        )
        {
            if (!TryResolveProTankiRoot(cacheRootPath, out string rootDir))
            {
                return;
            }

            string cacheDataDir = Path.Combine(rootDir, "Cache", "Cache_Data");
            if (!Directory.Exists(cacheDataDir))
            {
                return;
            }

            string? expectedShadersHash = null;
            if (shadersEnabled)
            {
                _ = LoadShaderScriptPayload(shaderHotkey, out expectedShadersHash);
            }
            string? expectedSkinAnimHash = null;
            if (skinAnimEnabled)
            {
                _ = LoadSkinAnimScriptPayload(skinAnimHotkey, out expectedSkinAnimHash);
            }

            (string? corePath, _, _) = EnsureCacheDataBundlesIndexed(cacheDataDir);

            string? coreJs = null;
            if (corePath != null)
            {
                _ = TryReadZstdBundle(corePath, out _, out coreJs);
            }

            bool needsShadersUpdate =
                expectedShadersHash != null
                && coreJs != null
                && (!coreJs.Contains(expectedShadersHash, StringComparison.Ordinal)
                    || coreJs.Contains(SkinAnimStartMarker, StringComparison.Ordinal));

            bool needsSkinAnimUpdate =
                !shadersEnabled
                && expectedSkinAnimHash != null
                && coreJs != null
                && !coreJs.Contains(expectedSkinAnimHash, StringComparison.Ordinal);

            bool needsSkinAnimRemoval =
                !skinAnimEnabled
                && !shadersEnabled
                && coreJs != null
                && coreJs.Contains(SkinAnimStartMarker, StringComparison.Ordinal);

            if (needsShadersUpdate)
            {
                (bool ok, string? err) = ApplyShaders(cacheRootPath, shaderHotkey);
                if (!ok)
                {
                    Log.Warning("Auto-sync Shaders failed: {Error}", err);
                }
            }

            if (needsSkinAnimUpdate)
            {
                (bool ok, string? err) = ApplySkinAnimations(cacheRootPath, skinAnimHotkey);
                if (!ok)
                {
                    Log.Warning("Auto-sync Skin Animations failed: {Error}", err);
                }
            }

            if (needsSkinAnimRemoval)
            {
                (bool ok, string? err) = RestoreDefaultSkinAnimations(cacheRootPath);
                if (!ok)
                {
                    Log.Warning("Auto-cleanup Skin Animations failed: {Error}", err);
                }
            }

            SyncRepairCooldown(cacheRootPath, shadersEnabled);
        }

        private string GetPackIpcToken()
        {
            ISettingsService? svc =
                settingsService
                ?? App.ServiceProvider?.GetService(typeof(ISettingsService)) as ISettingsService;
            return svc?.Load().PackIpcToken ?? string.Empty;
        }

        private string? LoadShaderScriptPayload(string toggleHotkey)
        {
            return LoadShaderScriptPayload(toggleHotkey, out _);
        }

        private string? LoadShaderScriptPayload(string toggleHotkey, out string hashMarker)
        {
            hashMarker = string.Empty;
            string scriptPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets",
                "Pack",
                "protanki_pack.js"
            );
            if (!File.Exists(scriptPath))
            {
                return null;
            }

            string cleanKey = Core.Constants.NormalizeShaderHotkey(toggleHotkey);
            string encodedKey = JsonSerializer.Serialize(cleanKey);
            string safeKeyToken = encodedKey.Trim('"');
            string safeIpcToken = JsonSerializer.Serialize(GetPackIpcToken()).Trim('"');

            string animModuleJs = string.Empty;
            string animPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets",
                "Animations",
                "protanki_skin_animations.js"
            );
            if (File.Exists(animPath))
            {
                animModuleJs = File.ReadAllText(animPath, Encoding.UTF8);
            }

            string script = File.ReadAllText(scriptPath, Encoding.UTF8)
                .Replace("/*__TS_SKIN_ANIM_MODULE__*/", animModuleJs, StringComparison.Ordinal)
                .Replace("__TS_SHADER_HOTKEY__", safeKeyToken, StringComparison.Ordinal)
                .Replace("__TS_IPC_TOKEN__", safeIpcToken, StringComparison.Ordinal);

            hashMarker = $"{ShadersHashMarkerPrefix}{ComputeShortHash(script)}{ShadersHashMarkerSuffix}";
            return $"{ShadersStartMarker}\n{hashMarker}\n{script}\n{ShadersEndMarker}\n";
        }

        private static byte[] CompressZstdRaw(byte[] uncompressed)
        {
            using Compressor compressor = new(9);
            return compressor.Wrap(uncompressed).ToArray();
        }

        private static bool TryReadZstdBundle(string filePath, out byte[] rawBytes, out string jsText)
        {
            rawBytes = [];
            jsText = string.Empty;

            string fileName = Path.GetFileName(filePath);
            if (fileName.Contains('.'))
            {
                return false;
            }

            try
            {
                using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length < 4)
                {
                    return false;
                }

                byte[] magic = new byte[4];
                if (fs.Read(magic, 0, 4) != 4 || !magic.AsSpan().SequenceEqual(ZstdMagic))
                {
                    return false;
                }

                fs.Position = 0;
                rawBytes = new byte[fs.Length];
                fs.ReadExactly(rawBytes);

                byte[] decompressed = DecompressZstdStream(rawBytes);
                jsText = Encoding.UTF8.GetString(decompressed);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void WritePatchedZstdBundle(
            string cacheDataDir,
            string originalsCacheDataDir,
            string filePath,
            string fileName,
            byte[] origRawBytes,
            int currentDiskSize,
            string patchedJs,
            bool saveBackup = true
        )
        {
            if (saveBackup)
            {
                _ = Directory.CreateDirectory(originalsCacheDataDir);
                string backupFile = Path.Combine(originalsCacheDataDir, fileName);
                if (!File.Exists(backupFile))
                {
                    File.WriteAllBytes(backupFile, origRawBytes);
                }

                string data1Backup = Path.Combine(originalsCacheDataDir, "data_1");
                string data1Src = Path.Combine(cacheDataDir, "data_1");
                if (File.Exists(data1Src) && !File.Exists(data1Backup))
                {
                    File.Copy(data1Src, data1Backup, overwrite: false);
                }
            }

            string data1Path = Path.Combine(cacheDataDir, "data_1");

            byte[] patchedUtf8 = Encoding.UTF8.GetBytes(patchedJs);
            byte[] recompressed = CompressZstdForCache(patchedUtf8, currentDiskSize, out bool exactSizeMatched);

            if ((!exactSizeMatched || recompressed.Length != currentDiskSize) && File.Exists(data1Path))
            {
                UpdateData1FileSize(data1Path, fileName, recompressed.Length);
            }

            File.WriteAllBytes(filePath, recompressed);
        }

        private static bool TryResolveProTankiRoot(string cacheRootPath, out string rootDir)
        {
            rootDir = string.Empty;
            if (string.IsNullOrWhiteSpace(cacheRootPath))
            {
                return false;
            }

            string trimmed = cacheRootPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Directory.Exists(Path.Combine(trimmed, "Cache", "Cache_Data")))
            {
                rootDir = trimmed;
                return true;
            }

            string? parent = Directory.GetParent(trimmed)?.FullName;
            if (!string.IsNullOrEmpty(parent) && Directory.Exists(Path.Combine(parent, "Cache", "Cache_Data")))
            {
                rootDir = parent;
                return true;
            }

            return false;
        }

        private static byte[] DecompressZstdStream(byte[] compressed)
        {
            const int maxDecompressedBytes = 64 * 1024 * 1024;
            using MemoryStream input = new(compressed);
            using DecompressionStream ds = new(input);
            using MemoryStream output = new();
            byte[] buffer = new byte[81920];
            int totalRead = 0;
            int read;
            while ((read = ds.Read(buffer, 0, buffer.Length)) > 0)
            {
                totalRead += read;
                if (totalRead > maxDecompressedBytes)
                {
                    throw new InvalidDataException("Zstd decompressed stream exceeds maximum allowed size.");
                }
                output.Write(buffer, 0, read);
            }
            return output.ToArray();
        }

        private static byte[] CompressZstdForCache(byte[] uncompressed, int targetSize, out bool exactSizeMatched)
        {
            using Compressor compressor = new(9);
            byte[] compressed = compressor.Wrap(uncompressed).ToArray();

            if (compressed.Length == targetSize)
            {
                exactSizeMatched = true;
                return compressed;
            }

            if (compressed.Length + 8 <= targetSize)
            {
                int padPayloadLen = targetSize - compressed.Length - 8;
                byte[] padded = new byte[targetSize];
                Buffer.BlockCopy(compressed, 0, padded, 0, compressed.Length);
                BinaryPrimitives.WriteUInt32LittleEndian(padded.AsSpan(compressed.Length, 4), 0x184D2A50u);
                BinaryPrimitives.WriteUInt32LittleEndian(padded.AsSpan(compressed.Length + 4, 4), (uint)padPayloadLen);
                exactSizeMatched = true;
                return padded;
            }

            exactSizeMatched = false;
            return compressed;
        }

        private static void UpdateData1FileSize(string data1Path, string fileName, int newSize)
        {
            if (!fileName.StartsWith("f_", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!uint.TryParse(fileName.AsSpan(2), NumberStyles.HexNumber, null, out uint fileNum))
            {
                return;
            }

            uint targetAddr = 0x80000000u | fileNum;
            byte[] data = File.ReadAllBytes(data1Path);
            const int headerSize = 8192;
            const int blockSize = 256;
            const int entrySize = 96;

            for (int off = headerSize; off + entrySize <= data.Length; off += blockSize)
            {
                for (int stream = 0; stream < 4; stream++)
                {
                    uint addr = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off + ((14 + stream) * 4), 4));
                    if (addr == targetAddr)
                    {
                        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(off + ((10 + stream) * 4), 4), newSize);
                        uint hash = ComputeSuperFastHash(data.AsSpan(off, 92));
                        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(off + (23 * 4), 4), hash);
                        File.WriteAllBytes(data1Path, data);
                        return;
                    }
                }
            }
        }

        private static uint ComputeSuperFastHash(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                return 0;
            }

            uint hash = (uint)data.Length;
            int rem = data.Length & 3;
            int len = data.Length >> 2;
            int pos = 0;

            for (; len > 0; len--)
            {
                hash += BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(pos, 2));
                uint tmp = (uint)(BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(pos + 2, 2)) << 11) ^ hash;
                hash = (hash << 16) ^ tmp;
                hash += hash >> 11;
                pos += 4;
            }

            switch (rem)
            {
                case 3:
                    hash += BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(pos, 2));
                    hash ^= hash << 16;
                    hash ^= (uint)(sbyte)data[pos + 2] << 18;
                    hash += hash >> 11;
                    break;
                case 2:
                    hash += BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(pos, 2));
                    hash ^= hash << 11;
                    hash += hash >> 17;
                    break;
                case 1:
                    hash += (uint)(sbyte)data[pos];
                    hash ^= hash << 10;
                    hash += hash >> 1;
                    break;
            }

            hash ^= hash << 3;
            hash += hash >> 5;
            hash ^= hash << 4;
            hash += hash >> 17;
            hash ^= hash << 25;
            hash += hash >> 6;
            return hash;
        }

        private static void ClearCodeCacheJs(string rootDir)
        {
            string codeCacheJsDir = Path.Combine(rootDir, "Code Cache", "js");
            if (!Directory.Exists(codeCacheJsDir))
            {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(codeCacheJsDir))
            {
                string name = Path.GetFileName(file);
                if (name.Equals("index", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    File.Delete(file);
                }
                catch { }
            }
        }
    }
}
