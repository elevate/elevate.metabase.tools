namespace metabase_exporter
{
    public class MetabaseApiSettings
    {
        public string MetabaseApiUrl { get; set; }
        public string MetabaseApiUsername { get; set; }
        public string MetabaseApiPassword { get; set; }

        /// <summary>
        /// Optional. If defined, the API clients attempts to use this token instead of creating a new one.
        /// </summary>
        public string MetabaseInitialToken { get; set; }
    }
}
