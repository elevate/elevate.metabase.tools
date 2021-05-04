using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.Contracts;

namespace metabase_exporter
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json", optional: true)
               .AddEnvironmentVariables()
               .AddCommandLine(args)
               ;

            var rawConfig = builder.Build();
            var config = ParseConfig(rawConfig);
            var api = await InitApi(config.MetabaseApiSettings);
            await config.Switch(
                export: api.Export, 
                import: api.Import, 
                testQuestions: _ => api.TestQuestions());
        }

        static async Task Export(this MetabaseApi api, Config.Export export)
        {
            var state = await api.Export(export.ExcludePersonalCollections);
            var stateJson = JsonConvert.SerializeObject(state, Formatting.Indented);
            File.WriteAllText(path: export.OutputFilename, contents: stateJson);
            Console.WriteLine($"Exported current state for {export.MetabaseApiSettings.MetabaseApiUrl} to {export.OutputFilename}");
        }

        static async Task Import(this MetabaseApi api, Config.Import import)
        {
            var rawState = File.ReadAllText(import.InputFilename);
            var state = JsonConvert.DeserializeObject<MetabaseState>(rawState);
            await api.Import(state, import.DatabaseMapping);
            Console.WriteLine($"Done importing from {import.InputFilename} into {import.MetabaseApiSettings.MetabaseApiUrl}");
        }

        [Pure]
        static Config ParseConfig(IConfiguration rawConfig)
        {
            var command = rawConfig["Command"];
            if (StringComparer.InvariantCultureIgnoreCase.Equals(command, "import"))
            {
                var apiSettings = ParseApiSettings(rawConfig);
                var inputFilename = rawConfig["InputFilename"];
                if (string.IsNullOrEmpty(inputFilename))
                {
                    throw new Exception("Missing InputFilename config");
                }
                var databaseMapping = ParseDatabaseMapping(rawConfig);
                return new Config.Import(apiSettings, inputFilename, databaseMapping);
            }
            else if (StringComparer.InvariantCultureIgnoreCase.Equals(command, "export"))
            {
                var apiSettings = ParseApiSettings(rawConfig);
                var outputFilename = rawConfig["OutputFilename"];
                if (string.IsNullOrEmpty(outputFilename))
                {
                    throw new Exception("Missing OutputFilename config");
                }

                var excludePersonalCollections = string.IsNullOrEmpty(rawConfig["ExcludePersonalCollections"]) == false;

                return new Config.Export(apiSettings, outputFilename, excludePersonalCollections);
            }
            else if (StringComparer.InvariantCultureIgnoreCase.Equals(command, "test-questions"))
            {
                var apiSettings = ParseApiSettings(rawConfig);
                return new Config.TestQuestions(apiSettings);
            }
            throw new Exception($"Invalid command '{command}', must be either 'import' or 'export' or 'test-questions'");
        }

        static IReadOnlyDictionary<DatabaseId, DatabaseId> ParseDatabaseMapping(IConfiguration rawConfig)
        {
            var rawDatabaseMapping = rawConfig.GetSection("DatabaseMapping");
            if (rawDatabaseMapping == null)
            {
                throw new Exception("Missing section DatabaseMapping");
            }

            var dict = new Dictionary<DatabaseId, DatabaseId>();
            foreach (var kv in rawDatabaseMapping.GetChildren())
            {
                try
                {
                    var key = new DatabaseId(int.Parse(kv.Key));
                    var value = new DatabaseId(int.Parse(kv.Value));
                    dict.Add(key, value);
                }
                catch (Exception e)
                {
                    throw new Exception($"Invalid database mapping: {kv.Key}->{kv.Value}", e);
                }
            }
            return dict;
        }

        [Pure]
        static MetabaseApiSettings ParseApiSettings(IConfiguration rawConfig)
        {
            var metabaseApiSection = rawConfig.GetSection("MetabaseApi");
            if (metabaseApiSection == null)
            {
                throw new Exception("Missing section 'MetabaseApi' in config");
            }
            var rawApiUrl = metabaseApiSection["Url"];
            if (rawApiUrl == null)
            {
                throw new Exception("Missing MetabaseApi:Url config");
            }

            Uri ParseUri()
            {
                try
                {
                    var apiUrl = new Uri(rawApiUrl.Trim());
                    if (new[] { Uri.UriSchemeHttp, Uri.UriSchemeHttps }.Contains(apiUrl.Scheme) == false)
                    {
                        throw new Exception("Invalid Metabase Url " + apiUrl);
                    }
                    return apiUrl;
                }
                catch (Exception e)
                {
                    throw new Exception("Invalid Metabase:Url value: " + rawApiUrl, e);
                }
            }

            var username = metabaseApiSection["Username"];
            if (string.IsNullOrEmpty(username))
            {
                throw new Exception("Missing MetabaseApi:Username config");
            }
            var password = metabaseApiSection["Password"];
            if (string.IsNullOrEmpty(password))
            {
                throw new Exception("Missing MetabaseApi:Password config");
            }

            bool.TryParse(metabaseApiSection["IgnoreSSLErrors"], out var ignoreSSLErrors);

            var timeout = TryParseTimeout(metabaseApiSection["Timeout"]);

            return new MetabaseApiSettings(ParseUri(),
                MetabaseApiUsername: username.Trim(),
                MetabaseApiPassword: password.Trim(),
                IgnoreSSLErrors: ignoreSSLErrors,
                MetabaseApiTimeout: timeout);
        }

        static TimeSpan? TryParseTimeout(string input)
        {
            if (TimeSpan.TryParse(input, out var timeout))
                return timeout;
            return null;
        }

        static async Task<MetabaseApi> InitApi(MetabaseApiSettings apiSettings)
        {
            const string filename = "metabase-token.txt";
            string GetInitialToken()
            {
                try
                {
                    return File.ReadAllText(filename);
                }
                catch (Exception)
                {
                    return null;
                }
            }

            // get an existing token if available to work around Metabase throttling
            // https://github.com/metabase/metabase/issues/4979
            var MetabaseInitialToken = GetInitialToken();

            var metabaseSession = new MetabaseSessionTokenManager(apiSettings, MetabaseInitialToken);
            var api = new MetabaseApi(metabaseSession);
            try
            {
                await api.GetAllDashboards(); // attempt an API call to either validate or renew the session token
                var token = await metabaseSession.CurrentToken();
                File.WriteAllText(filename, token);

                return api;
            }
            catch (Exception e)
            {
                throw new Exception("Error initialising Metabase API for " + apiSettings.MetabaseApiUrl, e);
            }
        }
    }
}
