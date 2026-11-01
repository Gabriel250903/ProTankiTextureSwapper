using System.Text.Json.Serialization;

namespace TextureSwapper.Core.Models
{
    public class RemoteConfigModel
    {
        [JsonPropertyName("halloween_enabled")]
        public bool HalloweenEnabled { get; set; } = true;

        [JsonPropertyName("thematic_gold_enabled")]
        public bool ThematicGoldEnabled { get; set; } = true;
    }
}
