using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface ISwapService
    {
        string DetectCachePath();
        string SelectiveBackup(string cachePath, SkinModel skin);
        string SelectiveBackup(string cachePath, ShotEffectModel shotEffect);
        string? Swap(
            string cachePath,
            SkinModel skin,
            string? inGamePaintName = null,
            CancellationToken ct = default
        );
        Task<string?> SwapAsync(
            string cachePath,
            SkinModel skin,
            string? inGamePaintName = null,
            CancellationToken ct = default
        );
        string? SwapShotEffect(
            string cachePath,
            ShotEffectModel shotEffect,
            CancellationToken ct = default
        );
        Task<string?> SwapShotEffectAsync(
            string cachePath,
            ShotEffectModel shotEffect,
            CancellationToken ct = default
        );
        string? SwapBatch(
            string cachePath,
            IEnumerable<SkinModel> skins,
            string? inGamePaintName = null,
            CancellationToken ct = default
        );
        Task<string?> SwapBatchAsync(
            string cachePath,
            IEnumerable<SkinModel> skins,
            string? inGamePaintName = null,
            CancellationToken ct = default
        );
        bool RestoreFullCache(string cachePath);
        bool RestoreFromBackup(string cachePath, string backupPath);
        void PurgeOldBackups(int maxDays);
        void ClearCache(string cachePath);
    }
}
