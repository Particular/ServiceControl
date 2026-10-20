namespace ServiceControl.Audit.AcceptanceTests.PostgreSql;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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

        var bodyStoragePath = Directory.CreateTempSubdirectory("sc_audit_at_bodies_").FullName;
        bodyStoragePaths.Add(bodyStoragePath);

        return new Dictionary<string, string>
        {
            [EFPersistenceConfigurationBase.ConnectionStringKey] = connectionString,
            [EFPersistenceConfigurationBase.SchemaKey] = schema,
            ["MessageBody/StorageType"] = "FileSystem",
            ["MessageBody/FileSystem/StoragePath"] = bodyStoragePath
        };
    }

    public async Task Cleanup(CancellationToken cancellationToken = default)
    {
        try
        {
            while (schemas.TryTake(out var schema))
            {
                await TestSchema.Drop(connectionString, schema, cancellationToken);
            }
        }
        finally
        {
            while (bodyStoragePaths.TryTake(out var bodyStoragePath))
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
    readonly ConcurrentBag<string> bodyStoragePaths = [];
    string connectionString;
}
