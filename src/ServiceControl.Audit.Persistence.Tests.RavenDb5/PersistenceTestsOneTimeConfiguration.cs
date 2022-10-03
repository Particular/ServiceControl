namespace ServiceControl.Audit.Persistence.Tests
{
    using System.IO;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.RavenDb;
    using ServiceControl.Audit.Persistence.RavenDb5;

    partial class PersistenceTestsOneTimeConfiguration
    {
        public Task SetUp()
        {
            var dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "AuditData");
            serverUrl = $"http://localhost:{FindAvailablePort(33334)}";
            embeddedDatabase = EmbeddedDatabase.Start(dbPath, serverUrl, new AuditDatabaseConfiguration("audit"));
            return Task.CompletedTask;
        }

        public Task TearDown()
        {
            embeddedDatabase.Dispose();
            return Task.CompletedTask;
        }

        public PersistenceTestsConfiguration GetPerTestConfiguration()
        {
            return new PersistenceTestsConfiguration(serverUrl);
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

        string serverUrl;
        EmbeddedDatabase embeddedDatabase;
    }
}
