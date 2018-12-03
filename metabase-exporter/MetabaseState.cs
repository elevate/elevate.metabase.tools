using Newtonsoft.Json;

namespace metabase_exporter
{
    /// <summary>
    /// Root object for Metabase data for import/export operations 
    /// </summary>
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
