namespace ServiceControl.Audit.Persistence.Tests
{
    using System.IO;
    using System.Linq;
    using System.Net.NetworkInformation;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.RavenDb;
    using ServiceControl.Audit.Persistence.RavenDb5;

    class SharedEmbeddedServer
    {
        public static EmbeddedDatabase Instance
        {
            get
            {
                lock (lockObject)
                {
                    if (embeddedDatabase != null)
                    {
                        return embeddedDatabase;
                    }

                    var dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "AuditData");
                    var serverUrl = $"http://localhost:{FindAvailablePort(33334)}";
                    embeddedDatabase = EmbeddedDatabase.Start(dbPath, serverUrl, new AuditDatabaseConfiguration("audit"));

                    return embeddedDatabase;
                }
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

        static object lockObject = new object();
    }
}
