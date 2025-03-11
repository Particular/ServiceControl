namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.RavenDB;
    using TestHelper;

    [TestFixture]
    class EmbeddedLifecycleTests : PersistenceTestFixture
    {
        string logPath;
        string dbPath;

        public override async Task Setup()
        {
            SetSettings = s =>
            {
                dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "Embedded");
                logPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                var databaseMaintenancePort = PortUtility.FindAvailablePort(33335);

                s.PersisterSpecificSettings[RavenPersistenceConfiguration.DatabasePathKey] = dbPath;
                s.PersisterSpecificSettings[RavenPersistenceConfiguration.LogPathKey] = logPath;
                s.PersisterSpecificSettings[RavenPersistenceConfiguration.DatabaseMaintenancePortKey] = databaseMaintenancePort.ToString();
            };

            //make sure to stop the global instance first
            await SharedEmbeddedServer.Stop();

            await base.Setup();
        }

        [Test]
        public async Task Verify_embedded_database()
        {
            await DataStore.QueryKnownEndpoints(TestContext.CurrentContext.CancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(dbPath, Does.Exist);
                Assert.That(logPath, Does.Exist);
            });
        }
    }
}