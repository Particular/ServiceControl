namespace ServiceControl.AcceptanceTests.SqlServer;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.AcceptanceTests.TestSupport;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.SqlServer;
using ServiceControl.Persistence.Tests;

public class AcceptanceTestStorageConfiguration : IAcceptanceTestStorageConfiguration
{
    public string PersistenceType { get; } = "SQLServer";

    public async Task CustomizeSettings(Settings settings, CancellationToken cancellationToken = default)
    {
        schema = $"sc_at_{Guid.NewGuid():n}";
        connectionString = await SqlServerSharedContainer.GetConnectionStringAsync(cancellationToken).ConfigureAwait(false);
        await TestSchema.Create(connectionString, schema, cancellationToken).ConfigureAwait(false);

        bodyStoragePath = Directory.CreateTempSubdirectory("sc_at_bodies_").FullName;

        settings.PersisterSpecificSettings = new SqlServerPersisterSettings
        {
            ConnectionString = connectionString,
            Schema = schema,
            ErrorRetentionPeriod = TimeSpan.FromDays(10),
            BodyStorage = new FileSystemBodyStorageSettings { StoragePath = bodyStoragePath }
        };
    }

    public async Task Cleanup(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref cleanupStarted, 1) != 0)
        {
            return;
        }

        try
        {
            if (connectionString == null || schema == null)
            {
                return;
            }

            await TestSchema.Drop(connectionString, schema, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (bodyStoragePath != null)
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

    string connectionString;
    string schema;
    string bodyStoragePath;
    int cleanupStarted;
}
