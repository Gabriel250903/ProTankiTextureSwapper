using System.Net.Http;
using TextureSwapper.Models;

namespace TextureSwapper.Services.Interfaces
{
    public interface IUpdateService
    {
        bool IsOffline { get; }
        Task<HttpResponseMessage> GetWithRetryAsync(string url, int maxRetries = 3);
        Task<(List<SkinModel>? Skins, string? RawJson)> FetchRemoteSkinsFileAsync(string fileName);
        Task<(List<ShotEffectModel>? ShotEffects, string? RawJson)> FetchRemoteShotEffectsFileAsync(string fileName);
        Task<(List<InGamePaintModel>? Paints, string? RawJson)> FetchRemoteInGamePaintsFileAsync(string fileName);
        Task<GitHubReleaseModel?> CheckForAppUpdatesAsync();
        Task DownloadAndRunInstallerAsync(string downloadUrl, Action<double>? onProgress = null);
        Task EnsureAssetsExistAsync(SkinModel skin, Action<string>? onProgress = null);
    }
}
