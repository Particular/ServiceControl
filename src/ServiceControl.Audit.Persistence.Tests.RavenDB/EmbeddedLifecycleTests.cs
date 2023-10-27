﻿namespace ServiceControl.Audit.Persistence.Tests
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

                s.PersisterSpecificSettings[RavenDbPersistenceConfiguration.DatabasePathKey] = dbPath;
                s.PersisterSpecificSettings[RavenDbPersistenceConfiguration.LogPathKey] = logPath;
                s.PersisterSpecificSettings[RavenDbPersistenceConfiguration.DatabaseMaintenancePortKey] = databaseMaintenancePort.ToString();
            };

            //make sure to stop the global instance first
            await SharedEmbeddedServer.Stop();

            await base.Setup();
        }

        [Test]
        public async Task Verify_embedded_database()
        {
            await DataStore.QueryKnownEndpoints();

            DirectoryAssert.Exists(dbPath);
            DirectoryAssert.Exists(logPath);
        }
    }
}