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
            await config.Switch(api.Export, api.Import);
        }

        static async Task Export(this MetabaseApi api, Config.Export export)
        {
            var state = await api.Export();
            File.WriteAllText(path: export.OutputFilename, contents: state);
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
                    throw new Exception("Mising InputFilename config");
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
                    throw new Exception("Mising OutputFilename config");
                }
                return new Config.Export(apiSettings, outputFilename);
            }
            throw new Exception($"Invalid command '{command}', must be either 'import' or 'export'");
        }

        static IReadOnlyDictionary<int, int> ParseDatabaseMapping(IConfiguration rawConfig)
        {
            var rawDatabaseMapping = rawConfig.GetSection("DatabaseMapping");
            if (rawDatabaseMapping == null)
            {
                throw new Exception("Missing section DatabaseMapping");
            }

            var dict = new Dictionary<int, int>();
            foreach (var kv in rawDatabaseMapping.GetChildren())
            {
                try
                {
                    var key = int.Parse(kv.Key);
                    var value = int.Parse(kv.Value);
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

            return new MetabaseApiSettings(ParseUri(),
                metabaseApiUsername: username.Trim(),
                metabaseApiPassword: password.Trim());
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
                catch (Exception e)
                {
                    return null;
                }
            }

            // get an existing token if available to work around Metabase throttling
            // https://github.com/metabase/metabase/issues/4979
            var MetabaseInitialToken = GetInitialToken();

            var metabaseSession = new MetabaseSessionTokenManager(apiSettings, MetabaseInitialToken);
            var api = new MetabaseApi(metabaseSession);
            await api.GetAllDashboards(); // attempt an API call to either validate or renew the session token
            var token = await metabaseSession.CurrentToken();
            File.WriteAllText(filename, token);

            return api;
        }
    }
}
