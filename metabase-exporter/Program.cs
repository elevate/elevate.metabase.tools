using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.Contracts;

namespace metabase_exporter;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
            ;

        var rawConfig = builder.Build();
        var config = ParseConfig(rawConfig);
            
        _serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            //Formatting = Formatting.Indented // don't set this, it will mess checksums
            ContractResolver = new AltNameContractResolver(),
        });
        _indentedSerializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new AltNameContractResolver(),
        });
            
        var api = await InitApi(config.MetabaseApiSettings);
        await config.Switch(
            export: api.Export, 
            import: c => c.Merge ? api.ImportMerge(c) : api.Import(c),
            testQuestions: _ => api.TestQuestions(),
            delete: api.Delete
        );
    }

    static JsonSerializer _serializer;
    public static JsonSerializer Serializer => _serializer;

    static JsonSerializer _indentedSerializer;

    static async Task Export(this MetabaseApi api, Config.Export export)
    {
        var state = await api.Export(export.ExcludePersonalCollections);
        var stateJson = _indentedSerializer.SerializeObject(state);
        File.WriteAllText(path: export.OutputFilename, contents: stateJson);
        Console.WriteLine($"Exported current state for {export.MetabaseApiSettings.MetabaseApiUrl} to {export.OutputFilename}");
    }

    static async Task Import(this MetabaseApi api, Config.Import import)
    {
        var rawState = File.ReadAllText(import.InputFilename);
        var state = Program.Serializer.DeserializeObject<MetabaseState>(rawState);
        await api.Import(state, import.DatabaseMapping, import.IgnoredDatabases);
        Console.WriteLine($"Done importing from {import.InputFilename} into {import.MetabaseApiSettings.MetabaseApiUrl}");
    }

    static async Task ImportMerge(this MetabaseApi api, Config.Import import)
    {
        var rawState = File.ReadAllText(import.InputFilename);
        var state = JsonConvert.DeserializeObject<MetabaseState>(rawState);
        await api.ImportMerge(state, import.DatabaseMapping, import.IgnoredDatabases);
        Console.WriteLine($"Done importing from {import.InputFilename} into {import.MetabaseApiSettings.MetabaseApiUrl}");
    }

    static async Task Delete(this MetabaseApi api, Config.Delete delete)
    {
        var allCards = await api.GetAllCards();
        var allDashboards = await api.GetAllDashboards();

        var matchedCards = (
            from card in allCards
            join cardToDelete in delete.Cards on card.Id equals cardToDelete
            select card
        ).ToImmutableList();

        var matchedDashboards = (
            from dashboard in allDashboards
            join dashboardToDelete in delete.Dashboards on dashboard.Id equals dashboardToDelete
            select dashboard
        ).ToImmutableList();

        var cardsNotFound = delete.Cards.Except(matchedCards.Select(x => x.Id)).ToImmutableList();
        var dashboardsNotFound = delete.Dashboards.Except(matchedDashboards.Select(x => x.Id)).ToImmutableList();
        if (cardsNotFound.Count > 0 || dashboardsNotFound.Count > 0)
        {
            var cardsMessage = cardsNotFound.Count > 0
                ? $"{cardsNotFound.Count} cards not found: {string.Join(",", cardsNotFound)}"
                : "";
            var dashboardsMessage = dashboardsNotFound.Count > 0 
                ? $"{dashboardsNotFound.Count} dashboards not found: {string.Join(",", dashboardsNotFound)}"
                : "";
            Console.WriteLine(string.Join("\n", [cardsMessage, dashboardsMessage]));
            return;
        }
        
        foreach (var card in matchedCards)
        {
            Console.WriteLine($"Deleting card '{card.Name}'...");
            await api.DeleteCard(card.Id);
        }

        foreach (var dashboard in matchedDashboards)
        {
            Console.WriteLine($"Deleting dashboard '{dashboard.Name}'...");
            await api.DeleteDashboard(dashboard.Id);
        }
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

            bool.TryParse(rawConfig["Merge"], out var merge);
            var ignoreDatabases = ParseIdList<DatabaseId>(rawConfig, "IgnoreDatabases");
            var databaseMapping = ParseDatabaseMapping(rawConfig);
            return new Config.Import(apiSettings, inputFilename, merge, databaseMapping, ignoreDatabases);
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
        else if (StringComparer.InvariantCultureIgnoreCase.Equals(command, "delete"))
        {
            var apiSettings = ParseApiSettings(rawConfig);
            var cards = ParseIdList<CardId>(rawConfig, "Cards");
            var dashboards = ParseIdList<DashboardId>(rawConfig, "Dashboards");
            return new Config.Delete(apiSettings, cards, dashboards);
        }
        throw new Exception($"Invalid command '{command}', must be either 'import' or 'export' or 'test-questions' or 'delete'");
    }

    static IReadOnlyList<TId> ParseIdList<TId>(IConfiguration rawConfig, string configKey)
        where TId: INewTypeEq<TId, int>, new()
    {
        var rawValue = rawConfig[configKey];
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }
        return rawValue
            .Split(",")
            .Select(x => {
                try
                {
                    return new TId().New(int.Parse(x));
                }
                catch (Exception e)
                {
                    throw new Exception($"Invalid {configKey} value: '{x}'", e);
                }
            })
            .ToImmutableList();
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