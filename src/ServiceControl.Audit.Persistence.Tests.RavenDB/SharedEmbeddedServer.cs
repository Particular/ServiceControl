namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Persistence.RavenDB;
    using Raven.Client.ServerWide.Operations;
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

                embeddedDatabase = EmbeddedDatabase.Start(new DatabaseConfiguration("audit", 60, true, TimeSpan.FromMinutes(5), 120000, 5, new ServerConfiguration(dbPath, serverUrl, logPath, logsMode), TimeSpan.FromSeconds(60)));

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
