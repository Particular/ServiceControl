namespace ServiceControl.Audit.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Audit.Auditing;
using ServiceControl.Audit.Auditing.MessagesView;
using ServiceControl.Audit.Infrastructure;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.Entities;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
using ServiceControl.Infrastructure;
using ServiceControl.SagaAudit;

sealed class AuditDataStore(
    IServiceScopeFactory scopeFactory,
    EFPersisterSettings settings,
    IFullTextSearchDialect fullTextSearch,
    TimeProvider timeProvider) : DataStoreBase(scopeFactory, settings), IAuditMessagesViewDataStore, ISagaHistoryDataStore
{
    public const int TotalCountCap = 100_000;

    const int SagaHistoryCap = 50_000;

    public Task<QueryResult<IList<MessagesView>>> GetMessages(bool includeSystemMessages, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default) =>
        Page(messages => messages
            .IncludeSystemMessagesWhere(includeSystemMessages)
            .FilterBySentTimeRange(timeSentRange), pagingInfo, sortInfo, cancellationToken);

    public Task<QueryResult<IList<MessagesView>>> QueryMessages(string searchParam, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default) =>
        Page(messages => Search(messages, searchParam)
            .FilterBySentTimeRange(timeSentRange), pagingInfo, sortInfo, cancellationToken);

    public Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(string endpoint, string keyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default) =>
        Page(messages => Search(messages.Where(message => message.ReceivingEndpointName == endpoint), keyword)
            .FilterBySentTimeRange(timeSentRange), pagingInfo, sortInfo, cancellationToken);

    public Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(bool includeSystemMessages, string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default) =>
        Page(messages => messages
            .Where(message => message.ReceivingEndpointName == endpointName)
            .IncludeSystemMessagesWhere(includeSystemMessages)
            .FilterBySentTimeRange(timeSentRange), pagingInfo, sortInfo, cancellationToken);

    public Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken = default) =>
        Page(messages => messages.Where(message => message.ConversationId == conversationId), pagingInfo, sortInfo, cancellationToken);

    public Task<MessageBodyView> GetMessageBody(string messageId, CancellationToken cancellationToken = default) =>
        ExecuteQueryWithDbContext(async (dbContext, token) =>
        {
            IQueryable<AuditMessageEntity> candidates;
            if (AuditBodyId.TryParse(messageId, out var createdOn, out var uniqueMessageId))
            {
                candidates = dbContext.AuditMessages.Where(message => message.CreatedOn == createdOn && message.UniqueMessageId == uniqueMessageId);
            }
            else if (Guid.TryParse(messageId, out uniqueMessageId))
            {
                candidates = dbContext.AuditMessages.Where(message => message.UniqueMessageId == uniqueMessageId);
            }
            else
            {
                return MessageBodyView.NotFound();
            }

            var body = await candidates
                .AsNoTracking()
                .OrderByDescending(message => message.CreatedOn)
                .ThenByDescending(message => message.Id)
                .Select(message => new { message.CreatedOn, message.Id, message.BodyState, message.BodyText, message.BodyContentType, message.BodySize })
                .FirstOrDefaultAsync(token);

            if (body is null)
            {
                return MessageBodyView.NotFound();
            }

            return body.BodyState switch
            {
                BodyState.Stored => MessageBodyView.FromString(body.BodyText!, body.BodyContentType ?? "text/plain", body.BodySize, DataVersion.Compose(("row", $"{body.CreatedOn.Ticks}-{body.Id}"))),
                BodyState.None or BodyState.TooLarge => MessageBodyView.NoContent(),
                BodyState.NotText => MessageBodyView.NotFound(),
                _ => throw new InvalidOperationException($"Unknown body state {body.BodyState}")
            };
        }, cancellationToken);

    public Task<QueryResult<IList<AuditCount>>> QueryAuditCounts(string endpointName, CancellationToken cancellationToken = default) =>
        ExecuteQueryWithDbContext(async (dbContext, token) =>
        {
            var today = timeProvider.GetUtcNow().UtcDateTime.Date;
            var from = today.AddDays(-29);
            var to = today.AddDays(1);

            var ingestedFrom = from.AddDays(-1);

            var received = dbContext.AuditMessages.AsNoTracking().Where(message => message.ReceivingEndpointName == endpointName);

            var days = await received
                .Where(message => !message.IsSystemMessage
                    && message.CreatedOn >= ingestedFrom
                    && message.ProcessedAt >= from
                    && message.ProcessedAt < to)
                .GroupBy(message => message.ProcessedAt.Date)
                .Select(day => new { Date = day.Key, Count = day.LongCount() })
                .OrderBy(day => day.Date)
                .ToListAsync(token);

            List<AuditCount> counts = [.. days.Select(day => new AuditCount { UtcDate = DateTime.SpecifyKind(day.Date, DateTimeKind.Utc), Count = day.Count })];

            if (counts.Count == 0
                && !await received.AnyAsync(token)
                && await dbContext.AuditMessages.AnyAsync(message => message.SendingEndpointName == endpointName && message.CreatedOn >= ingestedFrom, token))
            {
                counts.Add(new AuditCount { UtcDate = today, Count = 0 });
            }

            return new QueryResult<IList<AuditCount>>(counts, QueryStatsInfo.Zero);
        }, cancellationToken);

    public Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid input, CancellationToken cancellationToken = default) =>
        ExecuteQueryWithDbContext(async (dbContext, token) =>
        {
            var snapshots = await dbContext.SagaSnapshots
                .AsNoTracking()
                .Where(snapshot => snapshot.SagaId == input)
                .OrderByDescending(snapshot => snapshot.FinishTime)
                .ThenByDescending(snapshot => snapshot.CreatedOn)
                .ThenByDescending(snapshot => snapshot.Id)
                .Take(SagaHistoryCap)
                .ToListAsync(token);

            if (snapshots.Count == 0)
            {
                return QueryResult<SagaHistory>.Empty();
            }

            var history = new SagaHistory
            {
                Id = input,
                SagaId = input,
                SagaType = snapshots[0].SagaType,
                Changes = [.. snapshots.Select(ToStateChange)]
            };

            var version = DataVersion.OverRows([("changes", snapshots.Count)], snapshots, snapshot => [snapshot.CreatedOn, snapshot.Id]);

            return new QueryResult<SagaHistory>(history, new QueryStatsInfo(version, snapshots.Count));
        }, cancellationToken);

    Task<QueryResult<IList<MessagesView>>> Page(
        Func<IQueryable<AuditMessageEntity>, IQueryable<AuditMessageEntity>> filter,
        PagingInfo pagingInfo,
        SortInfo sortInfo,
        CancellationToken cancellationToken) =>
        ExecuteQueryWithDbContext(async (dbContext, token) =>
        {
            var messages = filter(dbContext.AuditMessages.AsNoTracking());

            var total = await messages.Take(TotalCountCap).LongCountAsync(token);

            var rows = await messages
                .Sort(sortInfo)
                .Skip(pagingInfo.Offset)
                .Take(pagingInfo.PageSize)
                .ToMessageRows()
                .ToListAsync(token);

            IList<MessagesView> results = [.. rows.Select(row => row.ToMessagesView())];

            var version = DataVersion.OverRows([("messages", total)], rows, row => [row.CreatedOn, row.Id]);

            return new QueryResult<IList<MessagesView>>(results, new QueryStatsInfo(version, total));
        }, cancellationToken);

    IQueryable<AuditMessageEntity> Search(IQueryable<AuditMessageEntity> source, string searchTerms) =>
        string.IsNullOrWhiteSpace(searchTerms) ? source : fullTextSearch.Search(source, searchTerms);

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
