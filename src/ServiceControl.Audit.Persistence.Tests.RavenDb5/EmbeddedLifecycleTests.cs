namespace ServiceControl.Audit.Persistence.Tests
{
    using System.IO;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.RavenDb;

    [TestFixture]
    class EmbeddedLifecycleTests : PersistenceTestFixture
    {
        public override async Task Setup()
        {
            SetSettings = s =>
            {
                var dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "Embedded");
                var databaseMaintenancePort = FindAvailablePort(33335);

                s.PersisterSpecificSettings[RavenDbPersistenceConfiguration.DatabasePathKey] = dbPath;
                s.PersisterSpecificSettings[RavenDbPersistenceConfiguration.DatabaseMaintenancePortKey] = databaseMaintenancePort.ToString();
            };

            //make sure to stop the global instance first
            await SharedEmbeddedServer.Stop();

            await base.Setup();
        }

        [Test]
        public async Task CheckDataStoreAvailable()
        {
            await DataStore.QueryKnownEndpoints();
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
    }
}