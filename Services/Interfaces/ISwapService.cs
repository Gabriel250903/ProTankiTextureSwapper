using TextureSwapper.Models;

namespace TextureSwapper.Services.Interfaces
{
    public interface ISwapService
    {
        string DetectCachePath();
        void SelectiveBackup(string cachePath, SkinModel skin);
        string? Swap(string cachePath, SkinModel skin, string? inGamePaintName = null);
        string? SwapShotEffect(string cachePath, ShotEffectModel shotEffect);
        string? SwapBatch(string cachePath, IEnumerable<SkinModel> skins, string? inGamePaintName = null);
        bool RestoreFullCache(string cachePath);
        bool RestoreFromBackup(string cachePath, string backupPath);
        void PurgeOldBackups(int maxDays);
        void ClearCache(string cachePath);
    }
}
