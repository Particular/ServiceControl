namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Linq;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Migrations;
    using Microsoft.EntityFrameworkCore.Migrations.Operations;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.SqlServer;

    [TestFixture]
    class MigrationSqlIsSchemaAwareTests
    {
        [Test]
        public void Every_hand_written_migration_statement_is_schema_aware()
        {
            using var dbContext = new SqlServerAuditDbContextFactory().CreateDbContext([]);
            var migrations = dbContext.GetService<IMigrationsAssembly>();

            var unrecognised = migrations.Migrations
                .Select(migration => migrations.CreateMigration(migration.Value, dbContext.Database.ProviderName))
                .SelectMany(migration => migration.UpOperations.Concat(migration.DownOperations))
                .OfType<SqlOperation>()
                .Select(operation => operation.Sql)
                .Where(sql => !FullTextSearchSql.IsHandled(sql))
                .ToArray();

            Assert.That(unrecognised, Is.Empty,
                $"A migration runs SQL that {nameof(FullTextSearchSql)}.{nameof(FullTextSearchSql.Rewrite)} does not recognise. It would run against the default schema, whatever Database/Schema is set to. Add it to Rewrite, and to IsHandled if it needs no qualifying.");
        }
    }
}
