using System.Data;
using Docker.DotNet;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using metabase_exporter;
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
        var commonArgs = new Dictionary<string, string>
        {
            { "MetabaseApi:Url", $"http://{metabase.Value.Hostname}:{metabase.Value.GetMappedPublicPort(metabasePort)}" },
            { "MetabaseApi:Username", user },
            { "MetabaseApi:Password", password },
            { "MetabaseApi:IgnoreSSLErrors", "true" }
        };
        
        const string inputFilename = "metabase-state.json";
        await Program.Main(commonArgs.AddMany(new Dictionary<string, string>
        {
            { "Command", "import"},
            { "InputFilename", inputFilename},
            { "DatabaseMapping:1", "1"}
        }).ToCommandline());
        
        await Program.Main(commonArgs.AddMany(new Dictionary<string, string>
        {
            { "Command", "test-questions"},
        }).ToCommandline());
        
        const string outputFilename = "test.json";
        await Program.Main(commonArgs.AddMany(new Dictionary<string, string>
        {
            { "Command", "export"},
            { "OutputFilename", outputFilename},
        }).ToCommandline());

        var output = await File.ReadAllTextAsync(outputFilename);
        var input = await File.ReadAllTextAsync(inputFilename);
        Assert.Equal(input, output);
    }

    static string[] ToCommandline(this IDictionary<string, string> x) => 
        x.Select(kv => $"{kv.Key}={kv.Value}").ToArray();

    static Dictionary<string, string> AddMany(this IDictionary<string, string> a, IDictionary<string, string> b)
    {
        var r = new Dictionary<string, string>(a);
        foreach (var kv in b)
            r.Add(kv.Key, kv.Value);
        return r;
    }
    
    private static Lazy<PostgreSqlContainer> postgres = new(() =>
    {
        var container = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .Build();
        container.StartAsync().GetAwaiter().GetResult();
        return container;
    });

    private static Lazy<IContainer> metabase = new(() =>
    {
        using var docker = new DockerClientConfiguration().CreateClient();
        var privateIP = docker.Networks.InspectNetworkAsync("bridge").GetAwaiter().GetResult().IPAM.Config[0].Gateway;
        var connString = new NpgsqlConnectionStringBuilder(postgres.Value.GetConnectionString());
        var container = new ContainerBuilder()
            .WithImage("metabase/metabase:v0.39.7")
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
        container.StartAsync().GetAwaiter().GetResult();
        
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
        return container;
    });
 
    const string password = "123456";
    const string user = "mauricioscheffer@gmail.com";
    const int metabasePort = 3000;

}