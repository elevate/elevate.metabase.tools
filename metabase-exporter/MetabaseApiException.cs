using System;

namespace metabase_exporter
{
    public class MetabaseApiException: Exception
    {
        public MetabaseApiException(string message) : base(message)
        {
        }

        public MetabaseApiException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}