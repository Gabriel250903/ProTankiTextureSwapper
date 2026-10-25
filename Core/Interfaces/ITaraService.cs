using System.IO;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface ITaraService
    {
        List<TaraEntry> Unpack(string taraFilePath);
        List<TaraEntry> Unpack(byte[] taraData);
        Task<List<TaraEntry>> UnpackAsync(string taraFilePath, CancellationToken ct = default);
        Task<List<TaraEntry>> UnpackStreamAsync(
            Stream sourceStream,
            CancellationToken ct = default
        );
        void Pack(List<TaraEntry> entries, string outputPath);
        byte[] Pack(List<TaraEntry> entries);
        Task PackAsync(List<TaraEntry> entries, string outputPath, CancellationToken ct = default);
        Task PackStreamAsync(
            List<TaraEntry> entries,
            Stream destStream,
            CancellationToken ct = default
        );
    }
}
