namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.CompositeViews.Messages;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.Infrastructure;

public class MessagesViewDataStore(IServiceScopeFactory scopeFactory, IFullTextSearchDialect fullTextSearch) : DataStoreBase(scopeFactory), IMessagesViewDataStore
{
    public Task<QueryResult<IList<MessagesView>>> GetAllMessages(PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => dbContext.FailedMessages
            .AsNoTracking()
            .IncludeSystemMessagesWhere(includeSystemMessages)
            .FilterBySentTimeRange(timeSentRange)
            .ToPagedMessagesResult(pagingInfo, sortInfo, token), cancellationToken);

    public Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => dbContext.FailedMessages
            .AsNoTracking()
            .Where(message => message.ReceivingEndpointName == endpointName)
            .IncludeSystemMessagesWhere(includeSystemMessages)
            .FilterBySentTimeRange(timeSentRange)
            .ToPagedMessagesResult(pagingInfo, sortInfo, token), cancellationToken);

    // includeSystemMessages is unused here: a conversation is incomplete without the system messages that took part in it.
    public Task<QueryResult<IList<MessagesView>>> GetAllMessagesByConversation(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => dbContext.FailedMessages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .ToPagedMessagesResult(pagingInfo, sortInfo, token), cancellationToken);

    public Task<QueryResult<IList<MessagesView>>> GetAllMessagesForSearch(string searchTerms, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => Search(dbContext.FailedMessages.AsNoTracking(), searchTerms)
            .FilterBySentTimeRange(timeSentRange)
            .ToPagedMessagesResult(pagingInfo, sortInfo, token), cancellationToken);

    public Task<QueryResult<IList<MessagesView>>> SearchEndpointMessages(string endpointName, string searchKeyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => Search(dbContext.FailedMessages.AsNoTracking(), searchKeyword)
            .Where(message => message.ReceivingEndpointName == endpointName)
            .FilterBySentTimeRange(timeSentRange)
            .ToPagedMessagesResult(pagingInfo, sortInfo, token), cancellationToken);

    // Neither search hides system messages: a caller who searched
    // for something specific is not helped by hiding the message that matched it.
    IQueryable<FailedMessageEntity> Search(IQueryable<FailedMessageEntity> source, string searchTerms) =>
        string.IsNullOrWhiteSpace(searchTerms) ? source : fullTextSearch.Search(source, searchTerms);
}
