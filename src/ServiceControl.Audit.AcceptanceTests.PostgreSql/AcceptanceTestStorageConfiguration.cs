namespace ServiceControl.Audit.AcceptanceTests.PostgreSql;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Audit.AcceptanceTests.TestSupport;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.Tests;

public class AcceptanceTestStorageConfiguration : IAcceptanceTestStorageConfiguration
{
    public string PersistenceType { get; } = "PostgreSQL";

    public async Task<IDictionary<string, string>> CustomizeSettings(CancellationToken cancellationToken = default)
    {
        var schema = $"sc_at_{Guid.NewGuid():n}";

        connectionString = await PostgreSqlSharedContainer.GetConnectionStringAsync(cancellationToken);
        await TestSchema.Create(connectionString, schema, cancellationToken);

        schemas.Add(schema);

        return new Dictionary<string, string>
        {
            [EFPersistenceConfigurationBase.ConnectionStringKey] = connectionString,
            [EFPersistenceConfigurationBase.SchemaKey] = schema
        };
    }

    public async Task Cleanup(CancellationToken cancellationToken = default)
    {
        while (schemas.TryTake(out var schema))
        {
            await TestSchema.Drop(connectionString, schema, cancellationToken);
        }
    }

    // The runner holds this lock while it writes the persister settings to the process-wide AppSettings.
    public async Task<IDisposable> UseDatabaseLifecycleLock(CancellationToken cancellationToken = default)
    {
        await initializationLock.WaitAsync(cancellationToken);
        return new Release();
    }

    sealed class Release : IDisposable
    {
        public void Dispose() => initializationLock.Release();
    }

    static readonly SemaphoreSlim initializationLock = new(1, 1);
    readonly ConcurrentBag<string> schemas = [];
    string connectionString;
}
