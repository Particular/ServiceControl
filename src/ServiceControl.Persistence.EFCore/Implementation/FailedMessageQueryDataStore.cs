namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.Extensions.DependencyInjection;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Persistence.Infrastructure;

public class FailedMessageQueryDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IFailedMessageQueryDataStore
{
    public Task<QueryResult<IList<FailedMessageView>>> GetFailedMessages(string status, string modified, string queueAddress, PagingInfo pagingInfo, SortInfo sortInfo) =>
        throw new NotImplementedException();

    public Task<QueryStatsInfo> GetFailedMessagesStats(string status, string modified, string queueAddress) =>
        throw new NotImplementedException();

    public Task<QueryResult<IList<FailedMessageView>>> GetFailedMessagesByEndpoint(string status, string endpointName, string modified, PagingInfo pagingInfo, SortInfo sortInfo) =>
        throw new NotImplementedException();

    public Task<IDictionary<string, object>> GetFailedMessagesSummary() =>
        throw new NotImplementedException();

    public Task<FailedMessageView> GetLatestFailedMessageView(string failedMessageId) =>
        throw new NotImplementedException();

    public Task<FailedMessage> GetFailedMessage(string failedMessageId) =>
        throw new NotImplementedException();

    public Task<FailedMessage[]> GetFailedMessagesByIds(Guid[] ids) =>
        throw new NotImplementedException();
}
