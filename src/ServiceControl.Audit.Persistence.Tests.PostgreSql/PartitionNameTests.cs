namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.PostgreSql;

    [TestFixture]
    class PartitionNameTests
    {
        [Test]
        [SetCulture("th-TH")]
        public void Partition_names_keep_the_gregorian_year_whatever_the_culture() =>
            Assert.That(PostgreSqlAuditPartitionManager.PartitionName("audit_messages", new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc)), Is.EqualTo("audit_messages_20260928"));
    }
}
