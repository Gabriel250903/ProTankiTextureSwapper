namespace TextureSwapper.Core.Interfaces
{
    public interface IHudService
    {
        bool IsCenteredHudEnabled();
        bool IsLoaderPatched();
        Task<(bool Success, string? ErrorMessage)> ApplyCenteredHudAsync(CancellationToken cancellationToken = default);
        (bool Success, string? ErrorMessage) RestoreDefaultHud();
        Task<(bool Success, string? ErrorMessage)> InstallLoaderPatchAsync();
        string GetLocalStorePath();
        string GetGameInstallPath();
    }
}
