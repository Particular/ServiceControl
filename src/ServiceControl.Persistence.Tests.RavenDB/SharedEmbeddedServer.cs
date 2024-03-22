namespace ServiceControl.Persistence.Tests
{
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using NUnit.Framework;
    using Raven.Client.ServerWide.Operations;
    using ServiceControl.Persistence.RavenDB;
    using TestHelper;

    static class SharedEmbeddedServer
    {
        public static async Task<EmbeddedDatabase> GetInstance(IHostApplicationLifetime lifetime, CancellationToken cancellationToken = default)
        {
            if (embeddedDatabase != null)
            {
                return embeddedDatabase;
            }

            await semaphoreSlim.WaitAsync(cancellationToken);

            try
            {
                var dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "PrimaryData");
                var logPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Logs", "Primary");
                var logsMode = "Operations";

                var settings = new RavenPersisterSettings
                {
                    DatabasePath = dbPath,
                    LogPath = logPath,
                    LogsMode = logsMode,
                    DatabaseMaintenancePort = PortUtility.FindAvailablePort(RavenPersisterSettings.DatabaseMaintenancePortDefault)
                };

                embeddedDatabase = EmbeddedDatabase.Start(lifetime, settings);

                //make sure that the database is up
                using var documentStore = await embeddedDatabase.Connect(cancellationToken);

                var cleanupDatabases = new DirectoryInfo(dbPath)
                    .GetDirectories()
                    .Select(di => di.Name)
                    .Where(name => name.Length == 32)
                    .ToArray();

                if (cleanupDatabases.Length > 0)
                {
                    var cleanupOperation = new DeleteDatabasesOperation(new DeleteDatabasesOperation.Parameters { DatabaseNames = cleanupDatabases, HardDelete = true });
                    await documentStore.Maintenance.Server.SendAsync(cleanupOperation, CancellationToken.None);
                }

                return embeddedDatabase;
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public static async Task Stop(CancellationToken cancellationToken = default)
        {
            await semaphoreSlim.WaitAsync(cancellationToken);
            try
            {
                embeddedDatabase?.Dispose();
                embeddedDatabase = null;
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        static EmbeddedDatabase embeddedDatabase;
        static readonly SemaphoreSlim semaphoreSlim = new(1, 1);
    }
}