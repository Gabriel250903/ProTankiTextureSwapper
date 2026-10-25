using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services
{
    public sealed class PropArchiveService(ITaraService taraService, IPropComposer propComposer)
        : IPropArchiveService
    {
        private readonly ITaraService _taraService = taraService;
        private readonly IPropComposer _propComposer = propComposer;

        public async Task<PropBundle> LoadPropArchiveAsync(
            string taraPath,
            CancellationToken ct = default
        )
        {
            List<TaraEntry> entries = await _taraService.UnpackAsync(taraPath, ct);
            return new PropBundle(entries);
        }

        public async Task SaveModifiedPropArchiveAsync(
            string taraPath,
            PropBundle bundle,
            CancellationToken ct = default
        )
        {
            await _taraService.PackAsync(bundle.Entries, taraPath, ct);
        }
    }
}
