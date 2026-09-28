namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Linq;
    using Microsoft.EntityFrameworkCore;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.PostgreSql;

    [TestFixture]
    class AuditCountsQueryTests
    {
        [Test]
        public void Counts_are_grouped_by_the_utc_day()
        {
            using var dbContext = new PostgreSqlAuditDbContextFactory().CreateDbContext([]);

            var sql = dbContext.AuditMessages.GroupBy(message => message.ProcessedAt.Date).Select(day => day.Key).ToQueryString();

            Assert.That(sql, Does.Contain("date_trunc('day', a.processed_at, 'UTC')"));
        }
    }
}
