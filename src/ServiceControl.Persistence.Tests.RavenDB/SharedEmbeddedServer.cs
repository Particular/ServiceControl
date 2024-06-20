namespace ServiceControl.Persistence.Tests
{
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting.Internal;
    using Microsoft.Extensions.Logging.Abstractions;
    using NUnit.Framework;
    using Raven.Client.ServerWide.Operations;
    using ServiceControl.RavenDB;
    using TestHelper;

    static class SharedEmbeddedServer
    {
        public static async Task<EmbeddedDatabase> GetInstance(CancellationToken cancellationToken = default)
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

                // TODO: See if more refactoring can be done between this and the RavenPersisterSettings above
                var embeddedConfig = new EmbeddedDatabaseConfiguration(settings.ServerUrl, RavenPersisterSettings.DatabaseNameDefault, dbPath, logPath, logsMode);

                embeddedDatabase = EmbeddedDatabase.Start(embeddedConfig, new ApplicationLifetime(new NullLogger<ApplicationLifetime>()));

                //make sure that the database is up
                using var documentStore = await embeddedDatabase.Connect(cancellationToken);

                var databaseSetup = new ServiceControl.Persistence.RavenDB.DatabaseSetup(settings, documentStore);
                await databaseSetup.Execute(cancellationToken);

                string[] cleanupDatabases = new DirectoryInfo(dbPath)
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