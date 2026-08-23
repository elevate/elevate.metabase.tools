using System.Collections.Immutable;
using System.Text;
using Docker.DotNet;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using metabase_exporter;
using Newtonsoft.Json.Linq;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Tests;

public static class Tests
{

    [Fact]
    public static async Task ImportTestExport()
    {
        //Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");
        await using var metabase = await BuildMetabaseContainer();
        var commonArgs = BuildMetabaseApiSettings(metabase);
        var api = BuildApi(metabase);

        await Run(commonArgs, new Dictionary<string, string>
        {
            { "Command", "import"},
            { "InputFilename", inputFilename},
            { "DatabaseMapping:1", "1"}
        });

        await Run(commonArgs, new Dictionary<string, string>
        {
            { "Command", "test-questions"},
        });

        // `test-questions` only prints its results, so assert on them here instead
        await AssertAllQuestionsRun(api);

        var exported = await Export(commonArgs, "test.json");
        var input = await File.ReadAllTextAsync(inputFilename);
        CompareStates(expected: input, actual: exported);

        // importing again exercises the delete-everything-first path, which a fresh
        // instance never does
        await Run(commonArgs, new Dictionary<string, string>
        {
            { "Command", "import"},
            { "InputFilename", inputFilename},
            { "DatabaseMapping:1", "1"}
        });

        exported = await Export(commonArgs, "test-reimport.json");
        CompareStates(expected: input, actual: exported);

        await AssertExcludePersonalCollections(commonArgs);
        await AssertDelete(commonArgs, api);
    }

    /// <summary>
    /// `ExcludePersonalCollections` should drop personal collections from the export
    /// and leave everything else intact.
    /// </summary>
    static async Task AssertExcludePersonalCollections(IReadOnlyDictionary<string, string> commonArgs)
    {
        var fullState = ParseState(await Export(commonArgs, "test-with-personal.json"));
        Assert.Contains(fullState.Collections, c => c.Name.Contains("Personal Collection"));

        var state = ParseState(await Export(commonArgs, "test-no-personal.json",
            new Dictionary<string, string> { { "ExcludePersonalCollections", "1" } }));

        Assert.DoesNotContain(state.Collections, c => c.Name.Contains("Personal Collection"));
        Assert.Equal(fullState.Collections.Length - 1, state.Collections.Length);
        // nothing lives in the personal collection, so cards and dashboards are unaffected
        Assert.Equal(fullState.Cards.Length, state.Cards.Length);
        Assert.Equal(fullState.Dashboards.Length, state.Dashboards.Length);
    }

    /// <summary>
    /// The `delete` command deletes the requested cards/dashboards, and deletes
    /// nothing at all if any of the requested ids does not exist.
    /// </summary>
    static async Task AssertDelete(IReadOnlyDictionary<string, string> commonArgs, MetabaseApi api)
    {
        var before = ParseState(await Export(commonArgs, "test-before-delete.json"));

        // ids in an exported state are renumbered, so ask the API for the real ones
        var cardToDelete = Assert.Single(await api.GetAllCards(), c => c.Name == "orders by month");
        var dashboardToDelete = Assert.Single(await api.GetAllDashboards(), d => d.Name == "rich dashboard");

        // a delete referencing anything that doesn't exist must be a no-op,
        // including for the ids that do exist
        await Run(commonArgs, new Dictionary<string, string>
        {
            { "Command", "delete"},
            { "Cards", $"{cardToDelete.Id},999999"},
            { "Dashboards", dashboardToDelete.Id.ToString()},
        });

        var afterNoOp = ParseState(await Export(commonArgs, "test-after-noop-delete.json"));
        Assert.Equal(before.Cards.Length, afterNoOp.Cards.Length);
        Assert.Equal(before.Dashboards.Length, afterNoOp.Dashboards.Length);

        await Run(commonArgs, new Dictionary<string, string>
        {
            { "Command", "delete"},
            { "Cards", cardToDelete.Id.ToString()},
            { "Dashboards", dashboardToDelete.Id.ToString()},
        });

        var after = ParseState(await Export(commonArgs, "test-after-delete.json"));
        Assert.Equal(before.Cards.Length - 1, after.Cards.Length);
        Assert.Equal(before.Dashboards.Length - 1, after.Dashboards.Length);
        Assert.DoesNotContain(after.Cards, c => c.Name == "orders by month");
        Assert.DoesNotContain(after.Dashboards, d => d.Name == "rich dashboard");
        // collections are never deleted
        Assert.Equal(before.Collections.Length, after.Collections.Length);
    }

    [Fact]
    public static async Task ImportMergeExport()
    {
        await using var metabase = await BuildMetabaseContainer();
        var commonArgs = BuildMetabaseApiSettings(metabase);

        await Run(commonArgs, MergeImport(inputFilename));

        var exported = await Export(commonArgs, "merge.json");
        var input = await File.ReadAllTextAsync(inputFilename);
        CompareStates(expected: input, actual: exported);

        // import-merge should be idempotent
        await Run(commonArgs, MergeImport(inputFilename));

        exported = await Export(commonArgs, "merge-again.json");
        CompareStates(expected: input, actual: exported);

        await AssertMergeAppliesChanges(commonArgs, input);
    }

    /// <summary>
    /// Merging a changed state should update the matching cards/dashboards in place
    /// and create the new ones, rather than duplicating anything.
    /// </summary>
    static async Task AssertMergeAppliesChanges(IReadOnlyDictionary<string, string> commonArgs, string input)
    {
        var state = ParseState(input);

        // update an existing card. The shape of the result set is unchanged so that
        // Metabase has no reason to recompute result_metadata
        var updatedCard = Assert.Single(state.Cards, c => c.Name == "orders count");
        updatedCard.Description = "updated by merge";
        updatedCard.DatasetQuery.Native.Query = "select count(*) from ORDERS where TOTAL > 0";

        // update an existing dashboard
        var updatedDashboard = Assert.Single(state.Dashboards, d => d.Name == "rich dashboard");
        updatedDashboard.Description = "updated by merge";

        // add a new card and a new dashboard pointing at it. Both are appended so that
        // they get the highest ids in Metabase, and therefore stay last after the
        // renumbering that export does
        var newCardId = new CardId(state.Cards.Max(c => c.Id.Value) + 1);
        var newCard = new Card
        {
            Id = newCardId,
            Name = "brand new question",
            Description = "added by merge",
            Archived = false,
            ResultMetadata = updatedCard.ResultMetadata,
            DatabaseId = updatedCard.DatabaseId,
            CollectionId = updatedCard.CollectionId,
            Display = "table",
            DatasetQuery = new DatasetQuery
            {
                DatabaseId = updatedCard.DatasetQuery.DatabaseId,
                Type = "native",
                Native = new NativeQuery
                {
                    Query = "select count(*) from PEOPLE",
                    TemplateTags = new Dictionary<string, TemplateTag>(),
                },
            },
            VisualizationSettings = new JObject(),
        };
        var newDashboard = new Dashboard
        {
            Id = new DashboardId(state.Dashboards.Max(d => d.Id.Value) + 1),
            Name = "brand new dashboard",
            Description = "added by merge",
            Archived = false,
            CollectionId = updatedCard.CollectionId,
            Parameters = [],
            Cards =
            [
                new DashboardCard
                {
                    Id = new DashboardCardId(1),
                    Column = 0,
                    Row = 0,
                    SizeX = 4,
                    SizeY = 4,
                    CardId = newCardId,
                    ParameterMappings = [],
                    Series = [],
                    VisualizationSettings = new JObject(),
                },
            ],
        };
        state.Cards = [..state.Cards, newCard];
        state.Dashboards = [..state.Dashboards, newDashboard];

        const string changedFilename = "merge-changed-input.json";
        var changed = MetabaseJsonSerializer.IndentedSerializer.SerializeObject(state);
        await File.WriteAllTextAsync(changedFilename, changed);

        await Run(commonArgs, MergeImport(changedFilename));

        var exported = await Export(commonArgs, "merge-changed.json");
        CompareStates(expected: changed, actual: exported);

        // nothing was duplicated
        var exportedState = ParseState(exported);
        Assert.Equal(exportedState.Cards.Length, exportedState.Cards.Select(c => c.Name).Distinct().Count());
        Assert.Equal(exportedState.Dashboards.Length, exportedState.Dashboards.Select(d => d.Name).Distinct().Count());
    }

    static async Task AssertAllQuestionsRun(MetabaseApi api)
    {
        var cards = await api.GetAllCards();
        Assert.NotEmpty(cards);
        foreach (var card in cards)
        {
            var result = await api.RunCard(card.Id);
            Assert.True(result.Status == "completed", $"Question '{card.Name}' did not run: {result.Status}\n{result.Error}");
        }
    }

    static Dictionary<string, string> MergeImport(string filename) =>
        new()
        {
            { "Command", "import"},
            { "InputFilename", filename},
            { "Merge", true.ToString()},
            { "DatabaseMapping:1", "1"}
        };

    static Task Run(IReadOnlyDictionary<string, string> commonArgs, IDictionary<string, string> args) =>
        Program.Main(commonArgs.AddMany(args).ToCommandline());

    static async Task<string> Export(IReadOnlyDictionary<string, string> commonArgs, string outputFilename, IDictionary<string, string>? extraArgs = null)
    {
        var args = new Dictionary<string, string>
        {
            { "Command", "export"},
            { "OutputFilename", outputFilename},
        };
        foreach (var kv in extraArgs ?? new Dictionary<string, string>())
            args.Add(kv.Key, kv.Value);
        await Run(commonArgs, args);
        return await File.ReadAllTextAsync(outputFilename);
    }

    static MetabaseState ParseState(string json)
    {
        var state = MetabaseJsonSerializer.Serializer.DeserializeObject<MetabaseState>(json);
        Assert.NotNull(state);
        return state;
    }

    static void CompareStates(string expected, string actual)
    {
        var actualState = ParseState(actual);
        var expectedState = ParseState(expected);
        Assert.True(expectedState.Collections.Length == actualState.Collections.Length, $"Different collection length. Expected {expectedState.Collections.Length}, was {actualState.Collections.Length}");
        Assert.True(expectedState.Dashboards.Length == actualState.Dashboards.Length, $"Different dashboard length. Expected {expectedState.Dashboards.Length}, was {actualState.Dashboards.Length}");
        Assert.True(expectedState.Cards.Length == actualState.Cards.Length, $"Different card length. Expected {expectedState.Cards.Length}, was {actualState.Cards.Length}");
        foreach (var dashboard in expectedState.Dashboards)
        {
            var matchingDashboards = actualState.Dashboards.Where(d => d.Name == dashboard.Name).ToImmutableList();
            var matchingDashboard = Assert.Single(matchingDashboards);
            Assert.True(dashboard.Cards.Length == matchingDashboard.Cards.Length, $"Different card length for dashboard '{dashboard.Name}'. Expected {dashboard.Cards.Length}, was {matchingDashboard.Cards.Length}");
        }

        expected = MetabaseJsonSerializer.Serializer.SerializeObject(expectedState);
        actual = MetabaseJsonSerializer.Serializer.SerializeObject(actualState);

        Assert.Equal(expected, actual);
    }

    static IReadOnlyDictionary<string, string> BuildMetabaseApiSettings(IContainer metabase) =>
        new Dictionary<string, string>
        {
            { "MetabaseApi:Url", MetabaseUrl(metabase) },
            { "MetabaseApi:Username", user },
            { "MetabaseApi:Password", password },
            { "MetabaseApi:IgnoreSSLErrors", "true" }
        };

    static string MetabaseUrl(IContainer metabase) =>
        $"http://{metabase.Hostname}:{metabase.GetMappedPublicPort(metabasePort)}";

    /// <summary>
    /// A direct API client, for assertions that need the real (non-renumbered) Metabase ids.
    /// </summary>
    static MetabaseApi BuildApi(IContainer metabase) =>
        new MetabaseApi(new MetabaseSessionTokenManager(
            new MetabaseApiSettings(
                MetabaseApiUrl: new Uri(MetabaseUrl(metabase)),
                MetabaseApiUsername: user,
                MetabaseApiPassword: password,
                IgnoreSSLErrors: true,
                MetabaseApiTimeout: null),
            initialToken: null));

    static string[] ToCommandline(this IDictionary<string, string> x) =>
        x.Select(kv => $"{kv.Key}={kv.Value}").ToArray();

    static Dictionary<string, string> AddMany(this IReadOnlyDictionary<string, string> a, IDictionary<string, string> b)
    {
        var r = new Dictionary<string, string>(a);
        foreach (var kv in b)
            r.Add(kv.Key, kv.Value);
        return r;
    }

    static async Task<IContainer> BuildMetabaseContainer()
    {
        var postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .Build();
        await postgres.StartAsync();
        using var docker = new DockerClientConfiguration().CreateClient();
        var privateIP = docker.Networks.InspectNetworkAsync("bridge").GetAwaiter().GetResult().IPAM.Config[0].Gateway;
        var connString = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString());
        var container = new ContainerBuilder()
            .WithImage($"metabase/metabase:{MetabaseVersion}")
            .WithPortBinding(metabasePort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Metabase Initialization COMPLETE"))
            .WithEnvironment(new Dictionary<string, string>
            {
                {"MB_DB_TYPE", "postgres"},
                {"MB_DB_HOST", privateIP},
                {"MB_DB_PORT", connString.Port.ToString()},
                {"MB_DB_USER", connString.Username!},
                {"MB_DB_PASS", connString.Password!},
            })
            .Build();
        await container.StartAsync();

        using var conn = new NpgsqlConnection(connString.ToString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO public.core_user
            (email, first_name, last_name, password, password_salt, date_joined, last_login, is_superuser, is_active, reset_token, reset_triggered, updated_at)
            VALUES
            (@user, 'Mauricio', 'Scheffer', '$2a$10$C6OAB3k6QgM6.0e8Jqo71udz14w2zMSVt4x.fdo9DmhnZkxs2yz4a', 'a4d9fe2a-fbd9-44c6-a1a1-de3bf8c3be86', NOW(), NOW(), true, true, NULL, 0, NOW());

            INSERT INTO public.permissions_group_membership (user_id, group_id) VALUES (1, 2);
        ";
        cmd.Parameters.Add(new NpgsqlParameter { ParameterName = "user", Value = user });

        cmd.ExecuteNonQuery();
        await ArchiveSampleContent(container);
        return container;
    }

    /// <summary>
    /// Metabase 0.50 and later seed new instances with an "Examples" collection full of
    /// questions, which would otherwise turn up in every export and make the expected state
    /// depend on the Metabase version under test.
    /// MB_LOAD_SAMPLE_CONTENT=false is not usable for this: it also drops the Sample Database
    /// that the test questions query. So archive the collection instead. Archiving takes its
    /// questions with it, and the exporter ignores archived entities.
    /// </summary>
    static async Task ArchiveSampleContent(IContainer metabase)
    {
        using var http = new HttpClient { BaseAddress = new Uri(MetabaseUrl(metabase)) };
        var login = await http.PostAsync("/api/session", JsonContent(
            new JObject { ["username"] = user, ["password"] = password }));
        login.EnsureSuccessStatusCode();
        var token = (string?)JObject.Parse(await login.Content.ReadAsStringAsync())["id"];
        http.DefaultRequestHeaders.Add("X-Metabase-Session", token);

        var collections = JArray.Parse(await http.GetStringAsync("/api/collection"));
        // `is_sample` only exists on the versions that have sample content in the first place,
        // so fall back to the name for the rest.
        var sampleCollections = collections
            .Where(c => (bool?)c["is_sample"] == true || (string?)c["name"] == "Examples")
            .ToImmutableList();
        foreach (var collection in sampleCollections)
        {
            var response = await http.PutAsync($"/api/collection/{collection["id"]}",
                JsonContent(new JObject { ["archived"] = true }));
            response.EnsureSuccessStatusCode();
        }
    }

    static StringContent JsonContent(JObject body) =>
        new StringContent(body.ToString(), Encoding.UTF8, "application/json");

    /// <summary>
    /// The Metabase version to test against, as a Docker image tag.
    /// Override with the METABASE_VERSION environment variable to test another
    /// supported version, e.g. METABASE_VERSION=v0.47.13 dotnet test.
    /// CI runs this over every version listed in the workflow matrix.
    /// </summary>
    static string MetabaseVersion =>
        Environment.GetEnvironmentVariable("METABASE_VERSION") is { Length: > 0 } v
            ? v
            : defaultMetabaseVersion;

    const string defaultMetabaseVersion = "v0.48.13";
    const string inputFilename = "metabase-state.json";
    const string password = "123456";
    const string user = "mauricioscheffer@gmail.com";
    const int metabasePort = 3000;

}
