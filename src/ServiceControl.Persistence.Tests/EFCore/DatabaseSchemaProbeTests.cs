namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.DbContexts;

class DatabaseSchemaProbeTests : PersistenceTestBase
{
    [Test]
    public async Task A_migrated_database_passes()
    {
        await ServiceProvider.EnsureDatabaseSchemaIsCurrent();
    }

    // The newest migration is removed from the history table, which is what a worker started
    // against a database its owner has not migrated yet would find.
    [Test]
    public async Task A_database_behind_the_binary_refuses_to_start_and_names_setup()
    {
        using (var scope = ServiceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            var newest = (await dbContext.Database.GetAppliedMigrationsAsync()).Last();
            var isSqlServer = dbContext.Database.ProviderName!.Contains("SqlServer", StringComparison.OrdinalIgnoreCase);
            var historyTable = (isSqlServer, dbContext.Schema) switch
            {
                (true, null) => "[__EFMigrationsHistory]",
                (true, var schema) => $"[{schema}].[__EFMigrationsHistory]",
                (false, null) => "\"__EFMigrationsHistory\"",
                (false, var schema) => $"\"{schema}\".\"__EFMigrationsHistory\""
            };
            var idColumn = isSqlServer ? "[MigrationId]" : "migration_id";

            var sql = "DELETE FROM " + historyTable + " WHERE " + idColumn + " = {0}";
            await dbContext.Database.ExecuteSqlRawAsync(sql, newest);
        }

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => ServiceProvider.EnsureDatabaseSchemaIsCurrent());

        Assert.That(exception.Message, Does.Contain("Run setup on the instance that owns this database"));
    }
}
