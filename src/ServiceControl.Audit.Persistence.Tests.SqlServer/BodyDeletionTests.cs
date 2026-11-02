namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.SqlServer;

    [TestFixture]
    class BodyDeletionTests
    {
        [Test]
        public async Task An_expired_hour_deletes_the_bodies_of_that_hour()
        {
            var hour = new DateTime(2026, 10, 20, 13, 0, 0, DateTimeKind.Utc);
            var storage = new RecordingBodyStorage();

            await new SqlServerAuditPartitionManager().DeleteBodies(storage, hour);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(storage.DeletedHours, Is.EqualTo([hour]));
                Assert.That(storage.DeletedDays, Is.Empty);
            }
        }
    }
}
