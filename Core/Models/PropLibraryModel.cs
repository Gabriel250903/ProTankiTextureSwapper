namespace TextureSwapper.Core.Models
{
    public class PropLibraryModel
    {
        public string Base64Url { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<PropModel> Props { get; set; } = [];
    }
}
