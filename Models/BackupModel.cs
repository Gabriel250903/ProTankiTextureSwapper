namespace TextureSwapper.Models
{
    public class BackupModel
    {
        public string FolderName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; }
        public string FullPath { get; set; } = string.Empty;

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
