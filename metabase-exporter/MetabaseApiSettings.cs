using System;

namespace metabase_exporter;

public record MetabaseApiSettings(
    Uri MetabaseApiUrl,
    string MetabaseApiUsername,
    string MetabaseApiPassword,
    bool IgnoreSSLErrors,
    TimeSpan? MetabaseApiTimeout
);