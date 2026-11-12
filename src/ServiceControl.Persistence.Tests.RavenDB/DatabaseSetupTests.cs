namespace ServiceControl.Persistence.Tests.RavenDB
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Raven.Client.ServerWide.Operations;
    using ServiceControl.Persistence.RavenDB;

    [TestFixture]
    class DatabaseSetupTests : RavenPersistenceTestBase
    {
        [Test]
        public async Task Setup_in_maintenance_mode_should_be_skipped()
        {
            var settings = new RavenPersisterSettings
            {
                DatabaseName = $"maintenance-mode-{Guid.NewGuid():N}",
                ThroughputDatabaseName = $"maintenance-mode-throughput-{Guid.NewGuid():N}",
                MaintenanceMode = true
            };

            await new DatabaseSetup(settings, DocumentStore).Execute();

            var record = await DocumentStore.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(settings.DatabaseName));
            var throughputRecord = await DocumentStore.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(settings.ThroughputDatabaseName));

            Assert.That(record, Is.Null);
            Assert.That(throughputRecord, Is.Null);
        }
    }
}
