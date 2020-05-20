using System;

namespace metabase_exporter
{
    public class MetabaseApiSettings
    {
        public Uri MetabaseApiUrl { get; }
        public string MetabaseApiUsername { get; }
        public string MetabaseApiPassword { get; }
        public bool IgnoreSSLErrors { get; }
        public TimeSpan? MetabaseApiTimeout { get; }

        public MetabaseApiSettings(Uri metabaseApiUrl, string metabaseApiUsername, string metabaseApiPassword, bool ignoreSslErrors, TimeSpan? metabaseApiTimeout)
        {
            MetabaseApiUrl = metabaseApiUrl;
            MetabaseApiUsername = metabaseApiUsername;
            MetabaseApiPassword = metabaseApiPassword;
            IgnoreSSLErrors = ignoreSslErrors;
            MetabaseApiTimeout = metabaseApiTimeout;
        }
    }
}
