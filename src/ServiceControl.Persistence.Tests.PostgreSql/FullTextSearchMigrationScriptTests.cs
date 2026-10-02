namespace ServiceControl.Persistence.Tests;

using System.Linq;
using EFCore.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Entities;

/// <summary>
/// No-database checks that verify the new migration's Up/Down DDL order and the MessageId index.
/// These inspect migration operations directly from the design-time context factory.
/// </summary>
[TestFixture]
class FullTextSearchMigrationScriptTests
{
    [Test]
    public void Up_operations_drop_old_index_install_function_before_index_and_create_message_id_index()
    {
        var migration = CreateMigration();

        var upSql = migration.UpOperations.OfType<SqlOperation>().Select(o => o.Sql).ToArray();
        var upCreateIndex = migration.UpOperations.OfType<CreateIndexOperation>().ToArray();

        using (Assert.EnterMultipleScope())
        {
            // 1. Drop the old inline expression index first
            Assert.That(upSql[0], Is.EqualTo(FullTextSearchSql.DropIndex),
                "Up must drop the old expression index first");

            // 2. Install the function before referencing it
            Assert.That(upSql[1], Is.EqualTo(FullTextSearchSql.CreateFunction),
                "Up must create the function before the GIN index");

            // 3. Create the function-call GIN index
            Assert.That(upSql[2], Is.EqualTo(FullTextSearchSql.CreateFunctionIndex),
                "Up must create the function-call GIN index after the function");

            // 4. Create the MessageId B-tree index via EF
            Assert.That(upCreateIndex, Has.Length.EqualTo(1));
            Assert.That(upCreateIndex[0].Name, Is.EqualTo("ix_failed_messages_message_id"));
            Assert.That(upCreateIndex[0].Table, Is.EqualTo("failed_messages"));
            Assert.That(upCreateIndex[0].Columns, Is.EqualTo(new[] { "message_id" }));
        }
    }

    [Test]
    public void Down_operations_drop_index_before_function_drop_message_id_and_recreate_preceding_index()
    {
        var migration = CreateMigration();

        var downSql = migration.DownOperations.OfType<SqlOperation>().Select(o => o.Sql).ToArray();
        var downDropIndex = migration.DownOperations.OfType<DropIndexOperation>().ToArray();

        using (Assert.EnterMultipleScope())
        {
            // 1. Drop the function-call GIN index before dropping the function
            Assert.That(downSql[0], Is.EqualTo(FullTextSearchSql.DropIndex),
                "Down must drop the function-call GIN index first");

            // 2. Drop the function
            Assert.That(downSql[1], Is.EqualTo(FullTextSearchSql.DropFunction),
                "Down must drop the function after the GIN index");

            // 3. Drop the MessageId index
            Assert.That(downDropIndex, Has.Length.EqualTo(1));
            Assert.That(downDropIndex[0].Name, Is.EqualTo("ix_failed_messages_message_id"));

            // 4. Recreate the preceding inline expression index
            Assert.That(downSql[2], Is.EqualTo(FullTextSearchSql.CreateIndex),
                "Down must recreate the preceding inline expression index");
        }
    }

    [Test]
    public void Snapshot_includes_message_id_index_on_failed_messages()
    {
        using var dbContext = new PostgreSqlServiceControlDbContextFactory().CreateDbContext([]);
        var entityType = dbContext.Model.FindEntityType(typeof(FailedMessageEntity));

        var index = entityType?.GetIndexes().FirstOrDefault(i => i.GetDatabaseName() == "ix_failed_messages_message_id");

        Assert.That(index, Is.Not.Null, "The model snapshot must include the MessageId index on failed_messages");
    }

    static Migration CreateMigration()
    {
        using var dbContext = new PostgreSqlServiceControlDbContextFactory().CreateDbContext([]);
        var migrationsAssembly = dbContext.GetService<IMigrationsAssembly>();
        var migration = migrationsAssembly.CreateMigration(
            migrationsAssembly.Migrations.Single(m => m.Key.EndsWith("AddSearchDocumentFunction")).Value,
            dbContext.Database.ProviderName);
        return migration;
    }
}