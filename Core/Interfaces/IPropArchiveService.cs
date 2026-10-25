using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IPropArchiveService
    {
        Task<PropBundle> LoadPropArchiveAsync(string taraPath, CancellationToken ct = default);
        Task SaveModifiedPropArchiveAsync(
            string taraPath,
            PropBundle bundle,
            CancellationToken ct = default
        );
    }
}
