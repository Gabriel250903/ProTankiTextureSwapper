using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IBntArchiveService
    {
        List<TaraEntry> Unpack(ReadOnlySpan<byte> bntData);
        byte[] Pack(IReadOnlyList<TaraEntry> entries);
        void UpdateModelImagesXml(List<TaraEntry> entries, bool hasAlpha);
    }
}
