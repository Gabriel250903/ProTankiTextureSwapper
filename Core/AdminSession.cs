namespace TextureSwapper.Core
{
    public static class AdminSession
    {
        public static string? CurrentSecret { get; private set; }

        public static bool IsAuthenticated => !string.IsNullOrEmpty(CurrentSecret);

        public static void SetSession(string secret)
        {
            CurrentSecret = secret;
        }

        public static void ClearSession()
        {
            CurrentSecret = null;
        }
    }
}
