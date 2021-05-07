using System;
using System.Collections.Generic;

namespace metabase_exporter
{
    public abstract record Config
    {
        private Config(MetabaseApiSettings metabaseApiSettings)
        {
            MetabaseApiSettings = metabaseApiSettings;
        }

        public MetabaseApiSettings MetabaseApiSettings { get; }

        public abstract T Switch<T>(Func<Export, T> export, Func<Import, T> import, Func<TestQuestions, T> testQuestions);

        public sealed record Export: Config
        {
            public string OutputFilename { get; }
            public bool ExcludePersonalCollections { get; }

            public Export(MetabaseApiSettings metabaseApiSettings, string outputFilename, bool excludePersonalCollections) : base(metabaseApiSettings)
            {
                OutputFilename = outputFilename;
                ExcludePersonalCollections = excludePersonalCollections;
            }

            public override T Switch<T>(Func<Export, T> export, Func<Import, T> import, Func<TestQuestions, T> testQuestions) =>
                export(this);
        }

        public sealed record Import: Config
        {
            public string InputFilename { get; }
            public IReadOnlyDictionary<DatabaseId, DatabaseId> DatabaseMapping { get; }

            public Import(MetabaseApiSettings MetabaseApiSettings, string inputFilename, IReadOnlyDictionary<DatabaseId, DatabaseId> databaseMapping): base(MetabaseApiSettings)
            {
                InputFilename = inputFilename;
                DatabaseMapping = databaseMapping;
            }

            public override T Switch<T>(Func<Export, T> export, Func<Import, T> import, Func<TestQuestions, T> testQuestions) =>
                import(this);
        }
        
        public sealed record TestQuestions: Config
        {
            public TestQuestions(MetabaseApiSettings metabaseApiSettings) : base(metabaseApiSettings){}

            public override T Switch<T>(Func<Export, T> export, Func<Import, T> import, Func<TestQuestions, T> testQuestions) =>
                testQuestions(this);
        }
    }
}
