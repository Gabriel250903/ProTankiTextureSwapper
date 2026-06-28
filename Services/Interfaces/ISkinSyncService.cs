using TextureSwapper.Models;

namespace TextureSwapper.Services.Interfaces
{
    public interface ISkinSyncService
    {
        event Action<string> ProgressChanged;
        Task<(List<SkinModel> Skins, List<InGamePaintModel>? RemoteInGamePaints)> SyncAndLoadSkinsAsync();
        List<BackupModel> LoadBackups();
        List<InGamePaintModel> LoadInGamePaints();
    }
}
