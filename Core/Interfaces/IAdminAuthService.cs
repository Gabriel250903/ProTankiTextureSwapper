namespace TextureSwapper.Core.Interfaces
{
    public interface IAdminAuthService
    {
        bool IsAuthenticated { get; }
        string? CurrentSecret { get; }
        Task<(bool Success, string Message)> AuthenticateAsync(string password);
        Task<bool> TryAutoLoginAsync(string? encryptedSecret);
        string EncryptSecret(string secret);
        string? DecryptSecret(string encrypted);
        void Logout();
    }
}
