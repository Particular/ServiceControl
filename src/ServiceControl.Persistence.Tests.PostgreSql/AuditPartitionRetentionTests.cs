namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.CustomChecks;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Implementation.Audit;
using ServiceControl.Persistence.EFCore.PostgreSql.Audit;

class AuditPartitionRetentionTests : AuditRetentionTestBase
{
    [Test]
    public async Task An_expired_hours_partitions_are_dropped_from_both_tables()
    {
        var expired = await SeedHour(AuditHours.Truncate(Now - Retention).AddHours(-1));

        await RunRetentionSweep();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await PartitionExists("audit_messages", expired), Is.False);
            Assert.That(await PartitionExists("saga_snapshots", expired), Is.False);
        }
    }

    [Test]
    public async Task An_expired_partition_that_never_received_a_row_is_dropped_too()
    {
        var expired = AuditHours.Truncate(Now - Retention).AddHours(-1);
        await EnsurePartitions(expired, expired.AddHours(1));

        await RunRetentionSweep();

        Assert.That(await PartitionExists("audit_messages", expired), Is.False);
    }

    [Test]
    public async Task The_sweep_keeps_the_provisioned_window_ahead_of_the_clock()
    {
        AdvanceClock(TimeSpan.FromHours(30));

        await RunRetentionSweep();

        var end = await Query(dbContext => Partitions.NewestProvisionedHourEnd(dbContext));

        Assert.That(end, Is.EqualTo(AuditHours.Truncate(Now) + AuditHours.Lookahead));
    }

    [Test]
    public async Task The_provisioning_check_passes_while_the_window_is_ahead_and_fails_once_it_runs_short()
    {
        var check = new AuditPartitionCustomCheck(
            ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            Partitions,
            ServiceProvider.GetRequiredService<TimeProvider>(),
            EFSettings);

        var beforehand = await check.PerformCheck();

        AdvanceClock(AuditHours.Lookahead - AuditPartitionCustomCheck.Threshold + TimeSpan.FromHours(1));

        var afterwards = await check.PerformCheck();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(beforehand.HasFailed, Is.False, beforehand.FailureReason);
            Assert.That(afterwards.HasFailed, Is.True);
            Assert.That(afterwards.FailureReason, Does.Contain("retention sweep"));
        }
    }

    [Test]
    public async Task The_provisioning_check_passes_where_the_audit_data_is_remote()
    {
        EFSettings.HostsAuditData = false;
        var check = new AuditPartitionCustomCheck(
            ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            Partitions,
            ServiceProvider.GetRequiredService<TimeProvider>(),
            EFSettings);

        AdvanceClock(AuditHours.Lookahead);

        var result = await check.PerformCheck();

        Assert.That(result.HasFailed, Is.False, "a primary that holds no audit data has no partitions to keep ahead");
    }

    async Task<bool> PartitionExists(string table, DateTime hour)
    {
        var names = await Query(dbContext => dbContext.Database
            .SqlQueryRaw<string>("""
                SELECT c.relname AS "Value"
                FROM pg_inherits i
                JOIN pg_class c ON c.oid = i.inhrelid
                JOIN pg_class p ON p.oid = i.inhparent
                JOIN pg_namespace n ON n.oid = p.relnamespace
                WHERE p.relname = {0} AND n.nspname = {1}
                """, table, dbContext.Schema)
            .ToListAsync());

        return names.Contains(AuditHours.PartitionName(table, hour));
    }
}
