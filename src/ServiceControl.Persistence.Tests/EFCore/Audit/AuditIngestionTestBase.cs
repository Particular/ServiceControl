namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation.Audit;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.SagaAudit;

abstract class AuditIngestionTestBase : IngestionTestBase
{
    protected AuditIngestionTestBase() =>
        RegisterServices = services => services.AddSingleton<IBodyStoragePersistence>(RecordedBodies);

    protected InMemoryBodyStoragePersistence RecordedBodies { get; } = new();

    protected EFPersisterSettings EFSettings => (EFPersisterSettings)PersistenceSettings;

    protected DateTime IngestionHour => AuditHours.Truncate(Now);

    protected Task IngestAudit(params IngestedAudit[] audits) =>
        InBatch(async unitOfWork =>
        {
            foreach (var audit in audits)
            {
                await unitOfWork.Audit.RecordProcessedMessage(audit.ToProcessedMessage(), audit.Body);
            }
        });

    protected Task IngestSnapshots(params SagaSnapshot[] snapshots) =>
        InBatch(async unitOfWork =>
        {
            foreach (var snapshot in snapshots)
            {
                await unitOfWork.Audit.RecordSagaSnapshot(snapshot);
            }
        });

    protected async Task<AuditMessageEntity> GetAuditMessage(Guid uniqueMessageId)
    {
        var rows = await GetAuditMessages(uniqueMessageId);

        Assert.That(rows, Has.Count.EqualTo(1), $"Expected exactly one audit row for {uniqueMessageId}");

        return rows[0];
    }

    protected Task<List<AuditMessageEntity>> GetAuditMessages(Guid uniqueMessageId) =>
        Query(dbContext => dbContext.AuditMessages.AsNoTracking().Where(m => m.UniqueMessageId == uniqueMessageId).OrderBy(m => m.Id).ToListAsync());

    protected Task<int> CountAuditMessages() =>
        Query(dbContext => dbContext.AuditMessages.AsNoTracking().CountAsync());

    protected Task<List<SagaSnapshotEntity>> GetSagaSnapshots(Guid sagaId) =>
        Query(dbContext => dbContext.SagaSnapshots.AsNoTracking().Where(s => s.SagaId == sagaId).OrderBy(s => s.Id).ToListAsync());

    protected Task<List<KnownEndpointEntity>> GetKnownEndpoints(IReadOnlyCollection<Guid> ids) =>
        Query(dbContext => dbContext.KnownEndpoints.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync());

    protected Task<FailedMessageEntity> FindFailedMessage(Guid uniqueMessageId) =>
        Query(dbContext => dbContext.FailedMessages.AsNoTracking().SingleOrDefaultAsync(m => m.UniqueMessageId == uniqueMessageId));

    protected async Task<T> Query<T>(Func<ServiceControlDbContext, Task<T>> query)
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        return await query(dbContext);
    }
}
