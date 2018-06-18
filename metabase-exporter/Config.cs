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

        public abstract T Switch<T>(Func<Export, T> export, Func<Import, T> import);

        public sealed class Export: Config
        {
            public string OutputFilename { get; }

            public Export(MetabaseApiSettings MetabaseApiSettings, string outputFilename): base(MetabaseApiSettings)
            {
                OutputFilename = outputFilename;
            }

            public override T Switch<T>(Func<Export, T> export, Func<Import, T> import) =>
                export(this);
        }

        public sealed class Import: Config
        {
            public string InputFilename { get; }
            public IReadOnlyDictionary<int, int> DatabaseMapping { get; }

            public Import(MetabaseApiSettings MetabaseApiSettings, string inputFilename, IReadOnlyDictionary<int, int> databaseMapping): base(MetabaseApiSettings)
            {
                InputFilename = inputFilename;
                DatabaseMapping = databaseMapping;
            }

            public override T Switch<T>(Func<Export, T> export, Func<Import, T> import) =>
                import(this);
        }
    }
}
