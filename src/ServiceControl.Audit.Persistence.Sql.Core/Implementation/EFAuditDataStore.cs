namespace ServiceControl.Audit.Persistence.Sql.Core.Implementation;

using ServiceControl.Audit.Auditing;
using ServiceControl.Audit.Auditing.MessagesView;
using ServiceControl.Audit.Infrastructure;
using ServiceControl.Audit.Monitoring;
using ServiceControl.SagaAudit;

class EFAuditDataStore : IAuditDataStore
{
    static readonly QueryStatsInfo EmptyStats = new(string.Empty, 0);

    public Task<QueryResult<IList<KnownEndpointsView>>> QueryKnownEndpoints(CancellationToken cancellationToken)
        => Task.FromResult(new QueryResult<IList<KnownEndpointsView>>([], EmptyStats));

    public Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid input, CancellationToken cancellationToken)
        => Task.FromResult(QueryResult<SagaHistory>.Empty());

    public Task<QueryResult<IList<MessagesView>>> GetMessages(bool includeSystemMessages, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new QueryResult<IList<MessagesView>>([], EmptyStats));

    public Task<QueryResult<IList<MessagesView>>> QueryMessages(string searchParam, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new QueryResult<IList<MessagesView>>([], EmptyStats));

    public Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(string endpoint, string keyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new QueryResult<IList<MessagesView>>([], EmptyStats));

    public Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(bool includeSystemMessages, string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new QueryResult<IList<MessagesView>>([], EmptyStats));

    public Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken)
        => Task.FromResult(new QueryResult<IList<MessagesView>>([], EmptyStats));

    public Task<MessageBodyView> GetMessageBody(string messageId, CancellationToken cancellationToken)
        => Task.FromResult(MessageBodyView.NoContent());

    public Task<QueryResult<IList<AuditCount>>> QueryAuditCounts(string endpointName, CancellationToken cancellationToken)
        => Task.FromResult(new QueryResult<IList<AuditCount>>([], EmptyStats));
}
