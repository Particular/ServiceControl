namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using Microsoft.Extensions.Logging.Abstractions;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
    using ServiceControl.Audit.Persistence.EFCore.PostgreSql;

    [TestFixture]
    class BodyIdPrefixTests
    {
        [Test]
        public void A_dropped_partition_covers_the_bodies_of_its_whole_day()
        {
            var manager = new PostgreSqlAuditPartitionManager(NullLogger<PostgreSqlAuditPartitionManager>.Instance);
            var day = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

            var prefix = manager.BodyIdPrefix(day);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(prefix, Is.EqualTo("20261020"));
                Assert.That(AuditBodyId.Format(day.AddHours(23), Guid.NewGuid()), Does.StartWith(prefix));
                Assert.That(AuditBodyId.Format(day.AddDays(1), Guid.NewGuid()), Does.Not.StartWith(prefix));
            }
        }
    }
}
