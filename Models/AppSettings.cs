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
    }
}
