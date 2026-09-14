namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.CompositeViews.Messages;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation.Audit;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.Infrastructure;

// Every view is the union of a failed branch and an audit branch, filtered alike, merged under the
// precedence, paging and counting rules IMessagesViewDataStore states. See MessageViewUnion.
public class MessagesViewDataStore(IServiceScopeFactory scopeFactory, IFullTextSearchDialect fullTextSearch, EFPersisterSettings settings) : DataStoreBase(scopeFactory), IMessagesViewDataStore
{
    public Task<QueryResult<IList<MessagesView>>> GetAllMessages(PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default) =>
        ExecuteQueryWithDbContext((dbContext, token) => MessageViewUnion.ToPagedMessagesResult(
            Failed(dbContext)
                .IncludeSystemMessagesWhere(includeSystemMessages)
                .FilterBySentTimeRange(timeSentRange)
                .ToRows(),
            WhenHosted(Audited(dbContext)
                .IncludeSystemMessagesWhere(includeSystemMessages)
                .FilterBySentTimeRange(timeSentRange)
                .ToRows(dbContext)),
            pagingInfo, sortInfo, token), cancellationToken);

    public Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default) =>
        ExecuteQueryWithDbContext((dbContext, token) => MessageViewUnion.ToPagedMessagesResult(
            Failed(dbContext)
                .Where(message => message.ReceivingEndpointName == endpointName)
                .IncludeSystemMessagesWhere(includeSystemMessages)
                .FilterBySentTimeRange(timeSentRange)
                .ToRows(),
            WhenHosted(Audited(dbContext)
                .Where(message => message.ReceivingEndpointName == endpointName)
                .IncludeSystemMessagesWhere(includeSystemMessages)
                .FilterBySentTimeRange(timeSentRange)
                .ToRows(dbContext)),
            pagingInfo, sortInfo, token), cancellationToken);

    // includeSystemMessages is unused here: a conversation is incomplete without the system messages that took part in it.
    public Task<QueryResult<IList<MessagesView>>> GetAllMessagesByConversation(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, CancellationToken cancellationToken = default) =>
        ExecuteQueryWithDbContext((dbContext, token) => MessageViewUnion.ToPagedMessagesResult(
            Failed(dbContext)
                .Where(message => message.ConversationId == conversationId)
                .ToRows(),
            WhenHosted(Audited(dbContext)
                .Where(message => message.ConversationId == conversationId)
                .ToRows(dbContext)),
            pagingInfo, sortInfo, token), cancellationToken);

    public Task<QueryResult<IList<MessagesView>>> GetAllMessagesForSearch(string searchTerms, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default) =>
        ExecuteQueryWithDbContext((dbContext, token) => MessageViewUnion.ToPagedMessagesResult(
            Search(Failed(dbContext), searchTerms)
                .FilterBySentTimeRange(timeSentRange)
                .ToRows(),
            WhenHosted(Search(Audited(dbContext), searchTerms)
                .FilterBySentTimeRange(timeSentRange)
                .ToRows(dbContext)),
            pagingInfo, sortInfo, token), cancellationToken);

    public Task<QueryResult<IList<MessagesView>>> SearchEndpointMessages(string endpointName, string searchKeyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default) =>
        ExecuteQueryWithDbContext((dbContext, token) => MessageViewUnion.ToPagedMessagesResult(
            Search(Failed(dbContext), searchKeyword)
                .Where(message => message.ReceivingEndpointName == endpointName)
                .FilterBySentTimeRange(timeSentRange)
                .ToRows(),
            WhenHosted(Search(Audited(dbContext), searchKeyword)
                .Where(message => message.ReceivingEndpointName == endpointName)
                .FilterBySentTimeRange(timeSentRange)
                .ToRows(dbContext)),
            pagingInfo, sortInfo, token), cancellationToken);

    static IQueryable<FailedMessageEntity> Failed(ServiceControlDbContext dbContext) => dbContext.FailedMessages.AsNoTracking();

    static IQueryable<AuditMessageEntity> Audited(ServiceControlDbContext dbContext) => dbContext.AuditMessages.AsNoTracking();

    // A primary whose audit data is on a dedicated host queries only the tables it owns.
    IQueryable<MessageRow>? WhenHosted(IQueryable<MessageRow> audited) => settings.HostsAuditData ? audited : null;

    // Neither search hides system messages: a caller who searched
    // for something specific is not helped by hiding the message that matched it.
    IQueryable<FailedMessageEntity> Search(IQueryable<FailedMessageEntity> source, string searchTerms) =>
        string.IsNullOrWhiteSpace(searchTerms) ? source : fullTextSearch.Search(source, searchTerms);

    IQueryable<AuditMessageEntity> Search(IQueryable<AuditMessageEntity> source, string searchTerms) =>
        string.IsNullOrWhiteSpace(searchTerms) ? source : fullTextSearch.Search(source, searchTerms);
}
