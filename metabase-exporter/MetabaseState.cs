using Newtonsoft.Json;

namespace metabase_exporter
{
    public class MetabaseState
    {
        [JsonProperty("collections")]
        public Collection[] Collections { get; set; }

        [JsonProperty("dashboards")]
        public Dashboard[] Dashboards { get; set; }

        [JsonProperty("cards")]
        public Card[] Cards { get; set; }
    }
}
