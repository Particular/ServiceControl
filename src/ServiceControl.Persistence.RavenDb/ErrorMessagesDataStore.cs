namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations;
    using ServiceControl.Persistence.Infrastructure;

    class ErrorMessagesDataStore : IErrorMessageDataStore
    {
        readonly IDocumentStore documentStore;

        public ErrorMessagesDataStore(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        public async Task<QueryResult<IList<MessagesView>>> GetAllMessages(
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            bool includeSystemMessages
            )
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .IncludeSystemMessagesWhere(includeSystemMessages)
                    .Statistics(out var stats)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(
            string endpointName,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            bool includeSystemMessages
            )
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .IncludeSystemMessagesWhere(includeSystemMessages)
                    .Where(m => m.ReceivingEndpointName == endpointName)
                    .Statistics(out var stats)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> GetAllMessagesByConversation(
            string conversationId,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            bool includeSystemMessages
            )
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Where(m => m.ConversationId == conversationId)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> GetAllMessagesForSearch(
            string searchTerms,
            PagingInfo pagingInfo,
            SortInfo sortInfo
            )
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Search(x => x.Query, searchTerms)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(
            string searchTerms,
            string receivingEndpointName,
            PagingInfo pagingInfo,
            SortInfo sortInfo
            )
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Search(x => x.Query, searchTerms)
                    .Where(m => m.ReceivingEndpointName == receivingEndpointName)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<FailedMessage> FailedMessageFetch(string failedMessageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                return await session.LoadAsync<FailedMessage>(new Guid(failedMessageId))
                    .ConfigureAwait(false);
            }
        }

        public async Task FailedMessageMarkAsArchived(string failedMessageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var failedMessage = await session.LoadAsync<FailedMessage>(new Guid(failedMessageId))
                    .ConfigureAwait(false);

                if (failedMessage.Status != FailedMessageStatus.Archived)
                {
                    failedMessage.Status = FailedMessageStatus.Archived;
                }

                await session.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task<FailedMessage[]> FailedMessagesFetch(Guid[] ids)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.LoadAsync<FailedMessage>(ids.Cast<ValueType>())
                    .ConfigureAwait(false);
                return results.Where(x => x != null).ToArray();
            }
        }

        public async Task StoreFailedErrorImport(FailedErrorImport failure)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                await session.StoreAsync(failure)
                    .ConfigureAwait(false);

                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
        }

        public async Task<AbstractEditFailedMessagesManager> CreateEditFailedMessageManager(string failedMessageId)
        {
            var session = documentStore.OpenAsyncSession();
            var failedMessage = await session.LoadAsync<FailedMessage>(FailedMessage.MakeDocumentId(failedMessageId)).ConfigureAwait(false);
            return new EditFailedMessageManager(session, failedMessage);
        }
    }
}