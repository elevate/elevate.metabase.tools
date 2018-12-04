using System;
using System.Collections.Generic;

namespace metabase_exporter
{
    public abstract class Config
    {
        private Config(MetabaseApiSettings metabaseApiSettings)
        {
            MetabaseApiSettings = metabaseApiSettings;
        }

        public MetabaseApiSettings MetabaseApiSettings { get; }

        public abstract T Switch<T>(Func<Export, T> export, Func<Import, T> import, Func<TestQuestions, T> testQuestions);

        public sealed class Export: Config
        {
            public string OutputFilename { get; }

            public Export(MetabaseApiSettings MetabaseApiSettings, string outputFilename): base(MetabaseApiSettings)
            {
                OutputFilename = outputFilename;
            }

            public override T Switch<T>(Func<Export, T> export, Func<Import, T> import, Func<TestQuestions, T> testQuestions) =>
                export(this);
        }

        public sealed class Import: Config
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
        
        public sealed class TestQuestions: Config
        {
            public TestQuestions(MetabaseApiSettings metabaseApiSettings) : base(metabaseApiSettings){}

            public override T Switch<T>(Func<Export, T> export, Func<Import, T> import, Func<TestQuestions, T> testQuestions) =>
                testQuestions(this);
        }
    }
}
