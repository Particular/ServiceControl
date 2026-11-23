namespace ServiceControl.Migration.Tests;

using System;
using System.Collections.Concurrent;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Particular.ServiceControl;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.Tests;

static class MigrationTestTarget
{
    public const string SqlServer = "SQLServer";
    public const string PostgreSql = "PostgreSQL";

    /// <summary>
    /// The connection string for the target the persister type names.
    /// PostgreSQL starts a test container when no connection string is set.
    /// </summary>
    public static Task<string> ConnectionString(string persisterType) =>
        persisterType == PostgreSql
            ? PostgreSqlSharedContainer.GetConnectionStringAsync()
            // CI's SQL Server action publishes this variable, and the fallback is a local developer default.
            : Task.FromResult(Environment.GetEnvironmentVariable("ServiceControl_Persistence_SqlServer_ConnectionString")
                ?? "Server=localhost;Database=ServiceControl;Trusted_Connection=True;TrustServerCertificate=True");

    /// <summary>
    /// The variable that names the target schema. Set it to a NewSchema value before the Settings are built.
    /// </summary>
    public const string SchemaVariable = "SERVICECONTROL_DATABASE_SCHEMA";

    /// <summary>
    /// A schema name no other test uses, so each test starts with an empty checkpoint table.
    /// </summary>
    public static string NewSchema() => $"migration_test_{Guid.NewGuid():n}";

    /// <summary>
    /// Creates the target schema the settings name and every table in it, the way setup does.
    /// It also makes every later host built from these settings load each persister into one load context the whole test run shares.
    /// </summary>
    public static async Task ApplySchema(Settings settings, CancellationToken cancellationToken = default)
    {
        ShareLoadContexts(settings);

        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Logging.ClearProviders();
        hostBuilder.AddServiceControlInstallers(settings);

        // Never started, because nothing here needs a hosted service.
        using var host = hostBuilder.Build();
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>().ApplyMigrations(cancellationToken);
    }

    /// <summary>
    /// Makes every host built from these settings load each persister into one load context the whole test run shares.
    /// Call it on settings that build a host without going through ApplySchema first.
    /// </summary>
    public static void ShareLoadContexts(Settings settings)
    {
        // The settings keep the persister's settings object from the first host, and a second load context cannot cast it.
        // Shared across tests, because EF Core throws once more than twenty load contexts have each built their own service provider.
        var resolve = settings.AssemblyLoadContextResolver;
        settings.AssemblyLoadContextResolver = assemblyPath => loadContexts.GetOrAdd(assemblyPath, resolve);
    }

    /// <summary>
    /// Hands the target's checkpoint store to the callback, for seeding or changing rows before a command runs.
    /// </summary>
    public static async Task WithCheckpointStore(Settings settings, Func<IMigrationCheckpointStore, CancellationToken, Task> use, CancellationToken cancellationToken = default)
    {
        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Services.AddPersistence(settings);

        // Never started: starting it would run the retention sweeper and resolve services this test never registers.
        using var host = hostBuilder.Build();
        await use(host.Services.GetRequiredService<IMigrationCheckpointStore>(), cancellationToken);
    }

    /// <summary>
    /// Drops a schema that NewSchema named, with every table in it. Call it from TearDown.
    /// </summary>
    public static async Task DropSchema(string persisterType, string schema)
    {
        var connectionString = await ConnectionString(persisterType);

        if (persisterType != PostgreSql)
        {
            await TestSchema.Drop(connectionString, schema);
            return;
        }

        // The PostgreSQL TestSchema has the same name as the SQL Server one, so both cannot be compiled here.
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE", connection);
        await command.ExecuteNonQueryAsync();
    }

    static readonly ConcurrentDictionary<string, AssemblyLoadContext> loadContexts = new();
}
