using System.Text.Json.Serialization;

namespace Synapse.Installer.Shared.Model
{
    public class Reaction
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; }

        [JsonPropertyName("+1")]
        public int ThumbsUp { get; set; }

        [JsonPropertyName("-1")]
        public int ThumbsDown { get; set; }

        [JsonPropertyName("laugh")]
        public int Laugh { get; set; }

        [JsonPropertyName("hooray")]
        public int Hooray { get; set; }

        [JsonPropertyName("confused")]
        public int Confused { get; set; }

        [JsonPropertyName("heart")]
        public int Heart { get; set; }

        [JsonPropertyName("rocket")]
        public int Rocket { get; set; }

        [JsonPropertyName("eyes")]
        public int Eyes { get; set; }
    }
}
