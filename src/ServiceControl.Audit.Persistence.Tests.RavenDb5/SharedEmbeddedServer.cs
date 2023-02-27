namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.RavenDb;

    class SharedEmbeddedServer
    {
        public static async Task<EmbeddedDatabase> GetInstance(CancellationToken cancellationToken = default)
        {
            await semaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (embeddedDatabase != null)
                {
                    return embeddedDatabase;
                }

                var dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "AuditData");
                var logPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Logs");
                var logsMode = "Operations";
                var serverUrl = $"http://localhost:{FindAvailablePort(33334)}";

                embeddedDatabase = EmbeddedDatabase.Start(new DatabaseConfiguration("audit", 60, true, TimeSpan.FromMinutes(5), 120000, 5, new ServerConfiguration(dbPath, serverUrl, logPath, logsMode)));

                //make sure that the database is up
                while (true)
                {
                    try
                    {
                        using (await embeddedDatabase.Connect(cancellationToken).ConfigureAwait(false))
                        {
                            //no-op
                        }

                        return embeddedDatabase;
                    }
                    catch (Exception)
                    {
                        await Task.Delay(500, cancellationToken).ConfigureAwait(false);
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
            await semaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
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

        static int FindAvailablePort(int startPort)
        {
            var activeTcpListeners = IPGlobalProperties
                .GetIPGlobalProperties()
                .GetActiveTcpListeners();

            for (var port = startPort; port < startPort + 1024; port++)
            {
                var portCopy = port;
                if (activeTcpListeners.All(endPoint => endPoint.Port != portCopy))
                {
                    return port;
                }
            }

            return startPort;
        }

        static EmbeddedDatabase embeddedDatabase;
        static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
    }
}
