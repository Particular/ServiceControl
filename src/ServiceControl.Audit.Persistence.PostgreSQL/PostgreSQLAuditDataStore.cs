namespace ServiceControl.Audit.Persistence.PostgreSQL;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Audit.Auditing;
using ServiceControl.Audit.Auditing.MessagesView;
using ServiceControl.Audit.Infrastructure;
using ServiceControl.Audit.Monitoring;
using ServiceControl.Audit.Persistence;
using ServiceControl.SagaAudit;

class PostgreSQLAuditDataStore : IAuditDataStore
{
    public Task<MessageBodyView> GetMessageBody(string messageId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<QueryResult<IList<MessagesView>>> GetMessages(bool includeSystemMessages, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<QueryResult<IList<AuditCount>>> QueryAuditCounts(string endpointName, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<QueryResult<IList<KnownEndpointsView>>> QueryKnownEndpoints(CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<QueryResult<IList<MessagesView>>> QueryMessages(string searchParam, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(bool includeSystemMessages, string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(string endpoint, string keyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid input, CancellationToken cancellationToken) => throw new NotImplementedException();
}
