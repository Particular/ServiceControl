// ReSharper disable once CheckNamespace

namespace ServiceControl.Persistence.Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EFCore.PostgreSql;
using MessageFailures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Infrastructure;

public partial class PersistenceTestsContext : IPersistenceTestsContext
{
    IHost host;
    string databaseName;
    string bodyStoragePath;

    public void AdvanceClock(TimeSpan by) => FakeTime.Advance(by);

    public DateTime UtcNow => FakeTime.GetUtcNow().UtcDateTime;

    public async Task Setup(IHostApplicationBuilder hostBuilder)
    {
        databaseName = $"sc_test_{Guid.NewGuid():n}";

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(await PostgreSqlSharedContainer.GetConnectionStringAsync())
        {
            Database = databaseName
        };

        bodyStoragePath = Directory.CreateTempSubdirectory("sc_test_bodies_").FullName;

        PersistenceSettings = new PostgreSqlPersisterSettings
        {
            ConnectionString = connectionStringBuilder.ConnectionString,
            BodyStorage = new FileSystemBodyStorageSettings { StoragePath = bodyStoragePath }
        };

        var persistence = new PostgreSqlPersistenceConfiguration().Create(PersistenceSettings);

        persistence.AddPersistence(hostBuilder.Services);
        persistence.AddInstaller(hostBuilder.Services);

        hostBuilder.Services.AddSingleton<TimeProvider>(FakeTime);
    }

    public async Task PostSetup(IHost host)
    {
        this.host = host;

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSqlServiceControlDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task TearDown()
    {
        DeleteBodyStorage();

        await using var connection = new NpgsqlConnection(await PostgreSqlSharedContainer.GetConnectionStringAsync());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
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

    void DeleteBodyStorage()
    {
        try
        {
            Directory.Delete(bodyStoragePath, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}
