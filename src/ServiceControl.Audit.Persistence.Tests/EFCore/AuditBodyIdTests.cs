namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

    [TestFixture]
    class AuditBodyIdTests
    {
        static readonly DateTime Hour = new(2026, 10, 20, 13, 0, 0, DateTimeKind.Utc);

        [Test]
        public void An_hour_prefix_matches_only_that_hour()
        {
            var prefix = AuditBodyId.HourPrefix(Hour);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(prefix, Is.EqualTo("2026102013-"));
                Assert.That(AuditBodyId.Format(Hour, Guid.NewGuid()), Does.StartWith(prefix));
                Assert.That(AuditBodyId.Format(Hour.AddHours(1), Guid.NewGuid()), Does.Not.StartWith(prefix));
            }
        }

        [Test]
        public void A_day_prefix_matches_every_hour_of_that_day_and_no_other()
        {
            var prefix = AuditBodyId.DayPrefix(Hour.Date);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(prefix, Is.EqualTo("20261020"));
                Assert.That(AuditBodyId.Format(Hour.Date, Guid.NewGuid()), Does.StartWith(prefix));
                Assert.That(AuditBodyId.Format(Hour.Date.AddHours(23), Guid.NewGuid()), Does.StartWith(prefix));
                Assert.That(AuditBodyId.Format(Hour.Date.AddDays(1), Guid.NewGuid()), Does.Not.StartWith(prefix));
                Assert.That(AuditBodyId.Format(Hour.Date.AddHours(-1), Guid.NewGuid()), Does.Not.StartWith(prefix));
            }
        }
    }
}
