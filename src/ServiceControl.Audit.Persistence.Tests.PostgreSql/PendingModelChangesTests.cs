namespace ServiceControl.Audit.Persistence.Tests
{
    using Microsoft.EntityFrameworkCore;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.PostgreSql;

    [TestFixture]
    class PendingModelChangesTests
    {
        [Test]
        public void The_model_matches_the_migrations()
        {
            using var dbContext = new PostgreSqlAuditDbContextFactory().CreateDbContext([]);

            Assert.That(dbContext.Database.HasPendingModelChanges(), Is.False,
                "The model has changed and no migration matches it. Run 'dotnet ef migrations add <name>' in ServiceControl.Audit.Persistence.EFCore.PostgreSql.");
        }
    }
}
