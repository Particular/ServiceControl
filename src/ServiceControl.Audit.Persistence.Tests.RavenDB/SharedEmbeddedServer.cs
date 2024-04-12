namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.RavenDB;
    using TestHelper;

    class SharedEmbeddedServer
    {
        public static async Task<EmbeddedDatabase> GetInstance(CancellationToken cancellationToken = default)
        {
            await semaphoreSlim.WaitAsync(cancellationToken);

            try
            {
                if (embeddedDatabase != null)
                {
                    return embeddedDatabase;
                }

                var dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "AuditData");
                var logPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Logs");
                var logsMode = "Operations";
                var serverUrl = $"http://localhost:{PortUtility.FindAvailablePort(33334)}";

                embeddedDatabase = EmbeddedDatabase.Start(new DatabaseConfiguration("audit", 60, true, TimeSpan.FromMinutes(5), 120000, 5, new ServerConfiguration(dbPath, serverUrl, logPath, logsMode)));

                //make sure that the database is up
                while (true)
                {
                    try
                    {
                        using (await embeddedDatabase.Connect(cancellationToken))
                        {
                            //no-op
                        }

                        return embeddedDatabase;
                    }
                    catch (Exception e)
                    {
                        Log.Warn("Could not connect to database. Retrying in 500ms...", e);
                        await Task.Delay(500, cancellationToken);
                    }
                }
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
        static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
        static readonly ILog Log = LogManager.GetLogger(typeof(SharedEmbeddedServer));
    }
}
