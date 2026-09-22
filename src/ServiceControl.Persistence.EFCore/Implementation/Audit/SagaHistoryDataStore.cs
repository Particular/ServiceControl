namespace ServiceControl.Persistence.EFCore.Implementation.Audit;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.SagaAudit;

public class SagaHistoryDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), ISagaHistoryDataStore
{
    public Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid sagaId, PagingInfo pagingInfo, CancellationToken cancellationToken = default) =>
        ExecuteQueryWithDbContext(async (dbContext, token) =>
        {
            var snapshots = dbContext.SagaSnapshots.AsNoTracking().Where(snapshot => snapshot.SagaId == sagaId);

            var totalChanges = await snapshots.CountAsync(token);

            if (totalChanges == 0)
            {
                return QueryResult<SagaHistory>.Empty();
            }

            var page = await snapshots
                .OrderByDescending(snapshot => snapshot.FinishTime)
                .ThenByDescending(snapshot => snapshot.Id)
                .Skip(pagingInfo.Offset)
                .Take(pagingInfo.Next)
                .ToListAsync(token);

            var sagaType = page.Count > 0
                ? page[0].SagaType
                : await snapshots.OrderByDescending(snapshot => snapshot.FinishTime).Select(snapshot => snapshot.SagaType).FirstAsync(token);

            var history = new SagaHistory
            {
                Id = sagaId,
                SagaId = sagaId,
                SagaType = sagaType,
                Changes = [.. page.Select(ToStateChange)]
            };

            var version = DataVersion.OverRows([("changes", totalChanges)], page, snapshot => [snapshot.CreatedOn, snapshot.Id]);

            return new QueryResult<SagaHistory>(history, new QueryStatsInfo(version, totalChanges));
        }, cancellationToken);

    static SagaStateChange ToStateChange(SagaSnapshotEntity snapshot) => new()
    {
        StartTime = snapshot.StartTime,
        FinishTime = snapshot.FinishTime,
        Status = snapshot.Status,
        StateAfterChange = snapshot.StateAfterChange,
        InitiatingMessage = SagaSnapshotJson.ReadInitiatingMessage(snapshot.InitiatingMessageJson),
        OutgoingMessages = SagaSnapshotJson.ReadOutgoingMessages(snapshot.OutgoingMessagesJson),
        Endpoint = snapshot.Endpoint
    };
}
