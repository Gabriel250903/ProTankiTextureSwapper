using Wpf.Ui.Appearance;

namespace TextureSwapper.Models
{
    public class AppSettings
    {
        public ApplicationTheme Theme { get; set; } = ApplicationTheme.Dark;
        public int MaxBackupRetentionDays { get; set; } = 30;
        public string LastCheckedTime { get; set; } = "Never";
        public string LastUpdateStatus { get; set; } = "Idle";
        public string? CustomCachePath { get; set; }
        public string? LastSelectedCategory { get; set; }
        public string? LastSelectedItemName { get; set; }
        public string? LastSearchQuery { get; set; }
        public double UIScale { get; set; } = 1.0;
        public string AdminPasswordSalt { get; set; } = "DEFAULT_SALT_123";
        public string AdminPasswordHash { get; set; } = "19FFFAF056A656FB4A13BAB7F8829D8C6B35C7C197C9629C42E07A3F7981CB68";
    }
}
