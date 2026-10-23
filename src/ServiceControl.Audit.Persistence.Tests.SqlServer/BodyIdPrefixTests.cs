namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
    using ServiceControl.Audit.Persistence.EFCore.SqlServer;

    [TestFixture]
    class BodyIdPrefixTests
    {
        [Test]
        public void An_expired_hour_covers_only_the_bodies_of_that_hour()
        {
            var hour = new DateTime(2026, 10, 20, 13, 0, 0, DateTimeKind.Utc);

            var prefix = new SqlServerAuditPartitionManager().BodyIdPrefix(hour);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(prefix, Is.EqualTo("2026102013-"));
                Assert.That(AuditBodyId.Format(hour, Guid.NewGuid()), Does.StartWith(prefix));
                Assert.That(AuditBodyId.Format(hour.AddHours(1), Guid.NewGuid()), Does.Not.StartWith(prefix));
            }
        }
    }
}
