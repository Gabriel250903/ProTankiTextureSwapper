namespace TextureSwapper.Core.Interfaces
{
    public interface ITextureDownloadService
    {
        bool AreTexturesInstalled();
        Task<bool> DownloadAndExtractTexturesAsync(
            Action<string, double>? onProgress = null,
            CancellationToken cancellationToken = default
        );
    }
}
