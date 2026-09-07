// ReSharper disable once CheckNamespace

namespace ServiceControl.Persistence.Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EFCore.PostgreSql;
using MessageFailures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Infrastructure;

public partial class PersistenceTestsContext : IPersistenceTestsContext
{
    IHost host;
    string connectionString;
    string schema;
    string bodyStoragePath;

    public void AdvanceClock(TimeSpan by) => FakeTime.Advance(by);

    public DateTime UtcNow => FakeTime.GetUtcNow().UtcDateTime;

    public async Task Setup(IHostApplicationBuilder hostBuilder)
    {
        schema = $"sc_test_{Guid.NewGuid():n}";
        connectionString = await PostgreSqlSharedContainer.GetConnectionStringAsync();
        await TestSchema.Create(connectionString, schema);

        bodyStoragePath = Directory.CreateTempSubdirectory("sc_test_bodies_").FullName;

        PersistenceSettings = new PostgreSqlPersisterSettings
        {
            ConnectionString = connectionString,
            Schema = schema,
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
        await scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>().ApplyMigrations();
    }

    public async Task TearDown()
    {
        DeleteBodyStorage();

        await TestSchema.Drop(connectionString, schema);
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
