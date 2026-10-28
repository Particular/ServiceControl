namespace ServiceControl.AcceptanceTests.PostgreSql;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.AcceptanceTests.TestSupport;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.PostgreSql;
using ServiceControl.Persistence.Tests;

public class AcceptanceTestStorageConfiguration : IAcceptanceTestStorageConfiguration
{
    public string PersistenceType { get; } = "PostgreSQL";

    public async Task CustomizeSettings(Settings settings, CancellationToken cancellationToken = default)
    {
        var schema = $"sc_at_{Guid.NewGuid():n}";
        var bodyStoragePath = Directory.CreateTempSubdirectory("sc_at_bodies_").FullName;

        connectionString = await PostgreSqlSharedContainer.GetConnectionStringAsync(cancellationToken).ConfigureAwait(false);
        await TestSchema.Create(connectionString, schema, cancellationToken).ConfigureAwait(false);

        // A test that runs more than one scenario comes back through here, and the runner cleans up
        // after each one. Recording everything created, rather than keeping only the most recent,
        // is what stops the earlier schema being stranded in the shared database.
        schemas.Add(schema);
        bodyStoragePaths.Add(bodyStoragePath);

        settings.PersisterSpecificSettings = new PostgreSqlPersisterSettings
        {
            ConnectionString = connectionString,
            Schema = schema,
            ErrorRetentionPeriod = TimeSpan.FromDays(10),
            BodyStorage = new FileSystemBodyStorageSettings { StoragePath = bodyStoragePath }
        };
    }

    public async Task Cleanup(CancellationToken cancellationToken = default)
    {
        try
        {
            while (schemas.TryTake(out var schema))
            {
                await TestSchema.Drop(connectionString, schema, cancellationToken).ConfigureAwait(false);
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

    public Task<IDisposable> UseDatabaseLifecycleLock(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IDisposable>(NoOpDisposable.Instance);
    }

    sealed class NoOpDisposable : IDisposable
    {
        public static readonly NoOpDisposable Instance = new();

        public void Dispose()
        {
        }
    }

    readonly ConcurrentBag<string> schemas = [];
    readonly ConcurrentBag<string> bodyStoragePaths = [];
    string connectionString;
}
