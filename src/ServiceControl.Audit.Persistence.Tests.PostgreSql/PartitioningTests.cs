namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.DbContexts;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
    using ServiceControl.Audit.Persistence.EFCore.PostgreSql;

    class PartitioningTests : EFPersistenceTestFixture
    {
        [TestCase("audit_messages")]
        [TestCase("saga_snapshots")]
        public async Task Table_is_range_partitioned_on_created_on(string table)
        {
            var (strategy, partitionKey) = await WithDbContext(async (dbContext, token) => (
                await QueryScalar(dbContext, $"SELECT partstrat::text FROM pg_partitioned_table WHERE partrelid = '{dbContext.Schema}.{table}'::regclass", token),
                await QueryScalar(dbContext, $"""
                    SELECT a.attname
                    FROM pg_partitioned_table p
                    JOIN pg_attribute a ON a.attrelid = p.partrelid AND a.attnum = p.partattrs[0]
                    WHERE p.partrelid = '{dbContext.Schema}.{table}'::regclass
                    """, token)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(strategy, Is.EqualTo("r"), $"{table} is not range partitioned");
                Assert.That(partitionKey, Is.EqualTo("created_on"));
            }
        }

        [Test]
        public async Task Setup_provisions_whole_days_through_the_lookahead()
        {
            var end = await ProvisionedUntil();
            var hour = AuditHours.Truncate(configuration.TimeProvider.GetUtcNow().UtcDateTime);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(end, Is.EqualTo(end!.Value.Date), "a partition holds a whole day");
                Assert.That(end, Is.GreaterThanOrEqualTo(hour + AuditHours.Lookahead));
                Assert.That(end, Is.LessThan(hour + AuditHours.Lookahead + TimeSpan.FromDays(1)));
            }
        }

        [Test]
        public async Task The_provisioning_check_passes_while_the_window_is_ahead_and_fails_once_it_runs_short()
        {
            var check = ActivatorUtilities.CreateInstance<AuditPartitionCustomCheck>(ServiceProvider);

            var ahead = await check.PerformCheck();
            var end = await ProvisionedUntil();
            configuration.TimeProvider.Advance(end!.Value - configuration.TimeProvider.GetUtcNow().UtcDateTime - AuditPartitionCustomCheck.Threshold + TimeSpan.FromHours(1));
            var runningShort = await check.PerformCheck();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ahead.HasFailed, Is.False);
                Assert.That(runningShort.HasFailed, Is.True);
            }
        }

        Task<DateTime?> ProvisionedUntil() =>
            WithDbContext((dbContext, token) => ServiceProvider.GetRequiredService<IAuditPartitionManager>().ProvisionedUntil(dbContext, token));

        static async Task<string> QueryScalar(AuditDbContext dbContext, string sql, CancellationToken cancellationToken)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            return (await command.ExecuteScalarAsync(cancellationToken))?.ToString();
        }
    }
}
