namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Implementation.Audit;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.EFCore.Infrastructure.Metrics;

class AuditRetentionTests : AuditRetentionTestBase
{
    [Test]
    public async Task Drops_hours_wholly_past_the_cutoff_and_keeps_the_rest()
    {
        var cutoff = Now - Retention;
        var expired = await SeedHour(AuditHours.Truncate(cutoff).AddHours(-1));
        var ancient = await SeedHour(AuditHours.Truncate(cutoff).AddDays(-30));
        var straddling = await SeedHour(AuditHours.Truncate(cutoff));
        var live = await SeedHour(AuditHours.Truncate(Now));

        await RunRetentionSweep();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await HourIsGone(expired), Is.True, "the hour that ends before the cutoff");
            Assert.That(await HourIsGone(ancient), Is.True, "a much older hour");
            Assert.That(await HourIsGone(straddling), Is.False, "the hour the cutoff falls in still has rows within retention");
            Assert.That(await HourIsGone(live), Is.False);
        }
    }

    [Test]
    public async Task Deletes_an_expired_hours_bodies_by_prefix_before_its_rows()
    {
        var expired = await SeedHour(AuditHours.Truncate(Now - Retention).AddHours(-1), externalBodies: true);
        var live = await SeedHour(AuditHours.Truncate(Now), externalBodies: true);

        await RunRetentionSweep();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(RecordedBodies.DeletedPrefixes, Is.EqualTo(new[] { AuditBodyStorage.Prefix(expired) }));
            Assert.That(await HourIsGone(expired), Is.True);
            Assert.That(await HourIsGone(live), Is.False);
        }
    }

    [Test]
    public async Task A_failing_body_delete_leaves_the_hours_rows_for_the_next_sweep()
    {
        var expired = await SeedHour(AuditHours.Truncate(Now - Retention).AddHours(-1), externalBodies: true);
        RecordedBodies.FailDeletePrefixFor.Add(AuditBodyStorage.Prefix(expired));

        await RunRetentionSweep();

        Assert.That(await HourIsGone(expired), Is.False, "rows outlive a body that could not be deleted, so nothing is stranded");

        RecordedBodies.FailDeletePrefixFor.Clear();

        await RunRetentionSweep();

        Assert.That(await HourIsGone(expired), Is.True);
    }

    [Test]
    public async Task An_hour_larger_than_one_batch_is_removed_in_full()
    {
        var expired = await SeedHour(AuditHours.Truncate(Now - Retention).AddHours(-1), messages: 2500, snapshots: 1100);

        await RunRetentionSweep();

        Assert.That(await HourIsGone(expired), Is.True);
    }

    [Test]
    public async Task The_audit_pass_reports_its_outcome()
    {
        using var recorded = new RecordedRetentionMetrics(ServiceProvider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());
        await SeedHour(AuditHours.Truncate(Now - Retention).AddHours(-1));

        await RunRetentionSweep();

        var cycle = recorded.Cycles(RetentionEntity.Audit).Single();

        Assert.That(cycle.Result, Is.EqualTo("success"));
    }

    [Test]
    public async Task A_sweep_is_skipped_while_another_connection_holds_the_lock()
    {
        var expired = await SeedHour(AuditHours.Truncate(Now - Retention).AddHours(-1));

        await using (await ServiceProvider.GetRequiredService<IRetentionLock>().TryAcquire())
        {
            await RunRetentionSweep();

            Assert.That(await HourIsGone(expired), Is.False, "the other holder is sweeping, so this pass stands down");
        }

        await RunRetentionSweep();

        Assert.That(await HourIsGone(expired), Is.True);
    }
}
