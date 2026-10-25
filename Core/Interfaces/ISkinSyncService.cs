using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface ISkinSyncService
    {
        event Action<string> ProgressChanged;
        event Action<SkinModel>? SkinPreviewUpdated;
        Task<(
            List<SkinModel> Skins,
            List<InGamePaintModel>? RemoteInGamePaints
        )> SyncAndLoadSkinsAsync(CancellationToken cancellationToken = default);
        Task<List<ShotEffectModel>> SyncAndLoadShotEffectsAsync(
            CancellationToken cancellationToken = default
        );
        List<BackupModel> LoadBackups();
        List<InGamePaintModel> LoadInGamePaints();
    }
}
