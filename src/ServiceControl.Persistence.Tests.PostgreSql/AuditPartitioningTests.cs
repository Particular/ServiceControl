namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.DbContexts;

/// <summary>
/// The migration converts the audit tables to partitioned ones by cloning them, which succeeds even
/// if the PARTITION BY clause is lost. Nothing else notices until retention tries to drop a partition
/// that was never created, so the conversion is asserted directly.
/// </summary>
class AuditPartitioningTests : PersistenceTestBase
{
    [TestCase("audit_messages")]
    [TestCase("saga_snapshots")]
    public async Task Table_is_range_partitioned_on_created_on(string table)
    {
        using var scope = ServiceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        var strategy = await QueryScalar(dbContext,
            $"SELECT partstrat::text FROM pg_partitioned_table WHERE partrelid = '{table}'::regclass");

        var partitionKey = await QueryScalar(dbContext,
            $"""
             SELECT a.attname
             FROM pg_partitioned_table p
             JOIN pg_attribute a ON a.attrelid = p.partrelid AND a.attnum = p.partattrs[0]
             WHERE p.partrelid = '{table}'::regclass
             """);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(strategy, Is.EqualTo("r"), $"{table} is not range partitioned");
            Assert.That(partitionKey, Is.EqualTo("created_on"));
        }
    }

    static async Task<string> QueryScalar(ServiceControlDbContext dbContext, string sql)
    {
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        return (await command.ExecuteScalarAsync())?.ToString();
    }
}
