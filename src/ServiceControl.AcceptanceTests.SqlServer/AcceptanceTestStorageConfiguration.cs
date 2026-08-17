namespace ServiceControl.AcceptanceTests.SqlServer;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
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
        databaseName = $"sc_at_{Guid.NewGuid():n}";
        serverConnectionString = await SqlServerSharedContainer.GetConnectionStringAsync(cancellationToken).ConfigureAwait(false);

        var connectionStringBuilder = new SqlConnectionStringBuilder(serverConnectionString)
        {
            InitialCatalog = databaseName
        };

        bodyStoragePath = Directory.CreateTempSubdirectory("sc_at_bodies_").FullName;

        settings.PersisterSpecificSettings = new SqlServerPersisterSettings
        {
            ConnectionString = connectionStringBuilder.ConnectionString,
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
            if (serverConnectionString == null || databaseName == null)
            {
                return;
            }

            var connection = new SqlConnection(serverConnectionString);
            await using (connection.ConfigureAwait(false))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                var command = connection.CreateCommand();
                await using (command.ConfigureAwait(false))
                {
                    command.CommandText = $"""
                        IF DB_ID('{databaseName}') IS NOT NULL
                        BEGIN
                            ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                            DROP DATABASE [{databaseName}];
                        END
                        """;
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
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

    string serverConnectionString;
    string databaseName;
    string bodyStoragePath;
    int cleanupStarted;
}
