namespace ServiceControl.AcceptanceTests.RavenDB;

using System;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.AcceptanceTests.TestSupport;
using ServiceControl.Persistence.Tests;
using ServiceControl.RavenDB;

public class AcceptanceTestStorageConfiguration : IAcceptanceTestStorageConfiguration
{
    public string PersistenceType { get; } = "RavenDB";


    public async Task CustomizeSettings(Settings settings, CancellationToken cancellationToken = default)
    {
        databaseName = Guid.NewGuid().ToString("n");
        databaseInstance = await SharedEmbeddedServer.GetInstance(cancellationToken);

        settings.PersisterSpecificSettings = new RavenPersisterSettings
        {
            ErrorRetentionPeriod = TimeSpan.FromDays(10),
            ConnectionString = databaseInstance.ServerUrl,
            DatabaseName = databaseName,
            ThroughputDatabaseName = $"{databaseName}-throughput"
        };
    }

    public async Task Cleanup(CancellationToken cancellationToken = default)
    {
        if (databaseInstance == null)
        {
            return;
        }
        using var _ = await UseDatabaseLifecycleLock(cancellationToken);
        await databaseInstance.DeleteDatabase(databaseName, cancellationToken);
        await databaseInstance.DeleteDatabase($"{databaseName}-throughput", cancellationToken);
    }

    /// <summary>
    /// The shared server cannot perform database lifecycle operations in parallel, take this lock when you
    /// need to do one of these operations in a test.
    /// </summary>
    public async Task<IDisposable> UseDatabaseLifecycleLock(CancellationToken cancellationToken = default)
    {
        await databaseLifecycleLock.WaitAsync(cancellationToken);
        return Disposable.Create(() => databaseLifecycleLock.Release());
    }

    static readonly SemaphoreSlim databaseLifecycleLock = new SemaphoreSlim(1, 1);
    EmbeddedDatabase databaseInstance;
    string databaseName;
}