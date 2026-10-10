// ReSharper disable once CheckNamespace
namespace ServiceControl.Persistence.Tests;

using System.Linq;
using EFCore.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

class MigrationSqlIsSchemaAwareTests : PersistenceTestBase
{
    [Test]
    public void Every_hand_written_migration_statement_is_schema_aware()
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostgreSqlServiceControlDbContext>();
        var migrations = dbContext.GetService<IMigrationsAssembly>();

        var unrecognised = migrations.Migrations
            .Select(migration => migrations.CreateMigration(migration.Value, dbContext.Database.ProviderName))
            .SelectMany(migration => migration.UpOperations.Concat(migration.DownOperations))
            .OfType<SqlOperation>()
            .Select(operation => operation.Sql)
            .Where(sql => !FullTextSearchSql.IsHandled(sql))
            .ToArray();

        Assert.That(unrecognised, Is.Empty,
            $"A migration runs SQL that {nameof(FullTextSearchSql)}.{nameof(FullTextSearchSql.Rewrite)} does not recognise. It would run against the connection's search path, whatever Database/Schema is set to. Add it to Rewrite, and to IsHandled if it needs no qualifying.");
    }
}
