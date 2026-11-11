using System.Text.Json.Serialization;

namespace TextureSwapper.Core.Models
{
    public class RemoteConfigModel
    {
        [JsonPropertyName("seasonal_enabled")]
        public bool SeasonalEnabled { get; set; } = false;

        [JsonPropertyName("halloween_enabled")]
        public bool HalloweenEnabled { get; set; } = false;

        [JsonPropertyName("thematic_gold_enabled")]
        public bool ThematicGoldEnabled { get; set; } = false;
    }
}
