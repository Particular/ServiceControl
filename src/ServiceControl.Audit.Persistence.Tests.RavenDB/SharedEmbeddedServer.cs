namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting.Internal;
    using Microsoft.Extensions.Logging.Abstractions;
    using NUnit.Framework;
    using Persistence.RavenDB;
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
                if (embeddedDatabase != null)
                {
                    return embeddedDatabase;
                }

                var dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "AuditData");
                var logPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Logs", "Audit");
                var logsMode = "Operations";
                var serverUrl = $"http://localhost:{PortUtility.FindAvailablePort(33334)}";

                var databaseConfiguration = new DatabaseConfiguration("audit", 60, true, TimeSpan.FromMinutes(5), 120000, 5, 5, new ServerConfiguration(dbPath, serverUrl, logPath, logsMode), TimeSpan.FromSeconds(60), RavenPersistenceConfiguration.SearchEngineTypeDefault);
                var serverConfig = databaseConfiguration.ServerConfiguration;

                // TODO: See if more refactoring can be done in configuration classes
                var embeddedConfig = new EmbeddedDatabaseConfiguration(serverConfig.ServerUrl, databaseConfiguration.Name, serverConfig.DbPath, serverConfig.LogPath, serverConfig.LogsMode);

                embeddedDatabase = EmbeddedDatabase.Start(embeddedConfig, new ApplicationLifetime(new NullLogger<ApplicationLifetime>()));

                //make sure that the database is up
                using var documentStore = await embeddedDatabase.Connect(cancellationToken);

                var databaseSetup = new DatabaseSetup(databaseConfiguration);
                await databaseSetup.Execute(documentStore, cancellationToken);

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
