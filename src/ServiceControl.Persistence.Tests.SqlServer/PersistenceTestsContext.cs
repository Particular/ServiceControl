// ReSharper disable once CheckNamespace
namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using EFCore.SqlServer;
using MessageFailures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using ServiceControl.Persistence.EFCore.Infrastructure;

public partial class PersistenceTestsContext : IPersistenceTestsContext
{
    IHost host;
    string databaseName;

    public FakeTimeProvider FakeTime { get; } = new();

    public async Task Setup(IHostApplicationBuilder hostBuilder)
    {
        databaseName = $"sc_test_{Guid.NewGuid():n}";

        var connectionStringBuilder = new SqlConnectionStringBuilder(await SqlServerSharedContainer.GetConnectionStringAsync())
        {
            InitialCatalog = databaseName
        };

        PersistenceSettings = new SqlServerPersisterSettings
        {
            ConnectionString = connectionStringBuilder.ConnectionString
        };

        var persistence = new SqlServerPersistenceConfiguration().Create(PersistenceSettings);

        persistence.AddPersistence(hostBuilder.Services);
        persistence.AddInstaller(hostBuilder.Services);

        hostBuilder.Services.AddSingleton<TimeProvider>(FakeTime);
    }

    public async Task PostSetup(IHost host)
    {
        this.host = host;

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SqlServerServiceControlDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task TearDown()
    {
        await using var connection = new SqlConnection(await SqlServerSharedContainer.GetConnectionStringAsync());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID('{databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}];
            END
            """;
        await command.ExecuteNonQueryAsync();
    }

    // Drain every insert-only reconciler so that ingested data is visible to the data stores,
    // without waiting for the reconciler background services' timers.
    public async Task CompleteDatabaseOperation()
    {
        foreach (var reconciler in host.Services.GetServices<IHostedService>().OfType<InsertOnlyTableReconciler>())
        {
            await reconciler.ReconcileNow();
        }
    }

    public PersistenceSettings PersistenceSettings { get; set; }

    public string GenerateFailedMessageRecordId(string messageId) => messageId;

    public Task InsertFailedMessages(params FailedMessage[] messages) => InsertFailedMessagesDirect(host.Services, messages);
}