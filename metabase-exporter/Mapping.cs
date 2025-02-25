namespace metabase_exporter;

public record Mapping<T>(
    T Source,
    T Target
);