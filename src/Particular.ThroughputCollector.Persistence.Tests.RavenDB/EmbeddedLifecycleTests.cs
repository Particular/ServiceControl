namespace Particular.ThroughputCollector.Persistence.Tests.RavenDb
{

    //[TestFixture]
    //class EmbeddedLifecycleTests : PersistenceTestFixture
    //{
    //    string logPath;
    //    string dbPath;

    //    public override async Task Setup()
    //    {
    //        SetSettings = s =>
    //        {
    //            dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "Embedded");
    //            logPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    //            var databaseMaintenancePort = PortUtility.FindAvailablePort(33335);

    //            s.PersisterSpecificSettings[RavenPersistenceConfiguration.DatabasePathKey] = dbPath;
    //            s.PersisterSpecificSettings[RavenPersistenceConfiguration.LogPathKey] = logPath;
    //            s.PersisterSpecificSettings[RavenPersistenceConfiguration.DatabaseMaintenancePortKey] = databaseMaintenancePort.ToString();
    //        };

    //        //make sure to stop the global instance first
    //        await SharedEmbeddedServer.Stop();

    //        await base.Setup();
    //    }

    //    [Test]
    //    public async Task Verify_embedded_database()
    //    {
    //        await DataStore.QueryKnownEndpoints();

    //        DirectoryAssert.Exists(dbPath);
    //        DirectoryAssert.Exists(logPath);
    //    }
    //}
}