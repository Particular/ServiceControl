namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.PostgreSql;

    [TestFixture]
    class BodyDeletionTests
    {
        [Test]
        public async Task A_dropped_partition_deletes_the_bodies_of_its_whole_day()
        {
            var day = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
            var storage = new RecordingBodyStorage();

            await new PostgreSqlAuditPartitionManager(NullLogger<PostgreSqlAuditPartitionManager>.Instance).DeleteBodies(storage, day);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(storage.DeletedDays, Is.EqualTo([day]));
                Assert.That(storage.DeletedHours, Is.Empty);
            }
        }
    }
}
