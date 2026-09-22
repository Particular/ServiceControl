namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation.Audit;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.SagaAudit;

abstract class AuditRetentionTestBase : AuditIngestionTestBase
{
    protected static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    [SetUp]
    public void SetRetention() => EFSettings.AuditRetentionPeriod = Retention;

    protected Task RunRetentionSweep() =>
        ServiceProvider.GetServices<IHostedService>().OfType<RetentionSweeper>().Single().SweepNow(TestContext.CurrentContext.CancellationToken);

    protected IAuditPartitionManager Partitions => ServiceProvider.GetRequiredService<IAuditPartitionManager>();

    protected Task EnsurePartitions(DateTime fromHour, DateTime toHourExclusive) =>
        Query(async dbContext =>
        {
            await Partitions.EnsurePartitions(dbContext, fromHour, toHourExclusive);
            return true;
        });

    protected async Task<DateTime> SeedHour(DateTime hour, int messages = 2, int snapshots = 2, bool externalBodies = false)
    {
        await EnsurePartitions(hour, hour.AddHours(1));

        await Store(Enumerable.Range(0, messages).Select(_ => new AuditMessageEntity
        {
            CreatedOn = hour,
            UniqueMessageId = Guid.NewGuid(),
            ProcessedAt = hour,
            Status = MessageStatus.Successful,
            HeadersJson = "{}",
            BodyStoredExternally = externalBodies,
            BodySize = 0
        }).ToArray());

        await Store(Enumerable.Range(0, snapshots).Select(_ => new SagaSnapshotEntity
        {
            CreatedOn = hour,
            SagaId = Guid.NewGuid(),
            Status = SagaStateChangeStatus.Updated,
            StartTime = hour,
            FinishTime = hour,
            ProcessedAt = hour
        }).ToArray());

        return hour;
    }

    protected async Task<bool> HourIsGone(DateTime hour) =>
        !await Query(dbContext => dbContext.AuditMessages.AsNoTracking().AnyAsync(m => m.CreatedOn == hour))
        && !await Query(dbContext => dbContext.SagaSnapshots.AsNoTracking().AnyAsync(s => s.CreatedOn == hour));

    protected async Task Store<T>(params T[] entities) where T : class
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        dbContext.Set<T>().AddRange(entities);

        await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    }
}
