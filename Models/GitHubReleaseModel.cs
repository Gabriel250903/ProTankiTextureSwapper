using System.Text.Json.Serialization;

namespace TextureSwapper.Models
{
    public class GitHubReleaseModel
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public List<GitHubAssetModel> Assets { get; set; } = [];
    }
}
