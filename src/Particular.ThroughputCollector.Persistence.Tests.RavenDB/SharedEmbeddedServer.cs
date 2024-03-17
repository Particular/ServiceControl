﻿namespace Particular.ThroughputCollector.Persistence.Tests.RavenDb
{
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Raven.Client.Documents;
    using Raven.Client.ServerWide.Operations;
    using TestHelper;

    static class SharedEmbeddedServer
    {
        public static async Task<IDocumentStore> GetInstance(CancellationToken cancellationToken = default)
        {
            await semaphoreSlim.WaitAsync(cancellationToken);

            try
            {
                var dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "AuditData");
                var logPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Logs", "Audit");
                var serverUrl = $"http://localhost:{PortUtility.FindAvailablePort(33334)}";

                //embeddedDatabase = EmbeddedDatabase.Start(new DatabaseConfiguration("audit"));

                //make sure that the database is up
                IDocumentStore documentStore = null; //await embeddedDatabase.Connect(cancellationToken);

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

                return documentStore;
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
                //embeddedDatabase?.Dispose();
                //embeddedDatabase = null;
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        static readonly SemaphoreSlim semaphoreSlim = new(1, 1);
    }
}