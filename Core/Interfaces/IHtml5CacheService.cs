using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IHtml5CacheService
    {
        bool TryResolveHtml5Paths(
            string cacheRootPath,
            out string levelDbDir,
            out string blobDir
        );
        bool ResourceExists(string cacheRootPath, long resourceId);
        byte[]? ReadRawBntBytes(string cacheRootPath, long resourceId);
        List<TaraEntry>? ReadResourceEntries(string cacheRootPath, long resourceId);
        bool WriteRawBntBytes(
            string cacheRootPath,
            long resourceId,
            ReadOnlySpan<byte> bntBytes
        );
        bool WriteResourceEntries(
            string cacheRootPath,
            long resourceId,
            IReadOnlyList<TaraEntry> entries
        );
        bool EnsureAnimatedPaintEnginePatched(string cacheRootPath);
        bool RestoreAnimatedPaintEngine(string cacheRootPath);
        (bool ShadersEnabled, bool SkinAnimationsEnabled) GetAppliedScriptStatus(string cacheRootPath);
        bool IsShadersEnabled(string cacheRootPath);
        (bool Success, string? ErrorMessage) ApplyShaders(
            string cacheRootPath,
            string toggleHotkey = "F8"
        );
        (bool Success, string? ErrorMessage) RestoreDefaultShaders(string cacheRootPath);
        (bool Success, string? ErrorMessage) ApplySkinAnimations(
            string cacheRootPath,
            string toggleHotkey = "F9"
        );
        (bool Success, string? ErrorMessage) RestoreDefaultSkinAnimations(string cacheRootPath);
        bool IsShadersUpdatePending(string cacheRootPath, string shaderHotkey);
        void SyncAppliedScripts(
            string cacheRootPath,
            bool shadersEnabled,
            string shaderHotkey,
            bool skinAnimEnabled = false,
            string skinAnimHotkey = "F9"
        );
        void InvalidateCache();
    }
}
