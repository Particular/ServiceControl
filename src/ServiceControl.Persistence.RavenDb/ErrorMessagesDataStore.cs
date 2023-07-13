namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net;
    using System.Threading.Tasks;
    using System.Web.Http;
    using CompositeViews.Messages;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Operations;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Recoverability;
    using Raven.Abstractions.Extensions;

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

        public Task<IEditFailedMessagesManager> CreateEditFailedMessageManager()
        {
            var session = documentStore.OpenAsyncSession();
            var manager = new EditFailedMessageManager(session);
            return Task.FromResult((IEditFailedMessagesManager)manager);
        }

        public async Task<QueryResult<FailureGroupView>> GetFailureGroupView(string groupId, string status, string modified)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var document = await session.Advanced
                    .AsyncDocumentQuery<FailureGroupView, ArchivedGroupsViewIndex>()
                    .Statistics(out var stats)
                    .WhereEquals(group => group.Id, groupId)
                    .FilterByStatusWhere(status)
                    .FilterByLastModifiedRange(modified)
                    .FirstOrDefaultAsync()  // TODO: Was previously a to list with a linq to object FirstOrDefault, not sure if this works
                    .ConfigureAwait(false);

                return new QueryResult<FailureGroupView>(document, stats.ToQueryStatsInfo());
            }
        }

        public async Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var groups = session
                    .Query<FailureGroupView, ArchivedGroupsViewIndex>()
                    .Where(v => v.Type == classifier
                    );

                var results = await groups
                    .OrderByDescending(x => x.Last)
                    .Take(200) // only show 200 groups
                    .ToListAsync()
                    .ConfigureAwait(false);

                return results;
            }
        }

        public async Task<QueryResult<IList<FailedMessageView>>> ErrorGet(
            string status,
            string modified,
            string queueAddress,
            PagingInfo pagingInfo,
            SortInfo sortInfo
            )
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Advanced
                    .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .Statistics(out var stats)
                    .FilterByStatusWhere(status)
                    .FilterByLastModifiedRange(modified)
                    .FilterByQueueAddress(queueAddress)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                    .SelectFields<FailedMessageView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<FailedMessageView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryStatsInfo> ErrorsHead(
            string status,
            string modified,
            string queueAddress
            )
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var stats = await session.Advanced
                    .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .FilterByStatusWhere(status)
                    .FilterByLastModifiedRange(modified)
                    .FilterByQueueAddress(queueAddress)
                    .QueryResultAsync()
                    .ConfigureAwait(false);

                return stats.ToQueryStatsInfo();
            }
        }

        public async Task<QueryResult<IList<FailedMessageView>>> ErrorsByEndpointName(
            string status,
            string endpointName,
            string modified,
            PagingInfo pagingInfo,
            SortInfo sortInfo
            )
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Advanced
                    .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .Statistics(out var stats)
                    .FilterByStatusWhere(status)
                    .AndAlso()
                    .WhereEquals("ReceivingEndpointName", endpointName)
                    .FilterByLastModifiedRange(modified)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                    .SelectFields<FailedMessageView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<FailedMessageView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<IDictionary<string, object>> ErrorsSummary()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var facetResults = await session.Query<FailedMessage, FailedMessageFacetsIndex>()
                    .ToFacetsAsync(new List<Facet>
                    {
                        new Facet
                        {
                            Name = "Name",
                            DisplayName = "Endpoints"
                        },
                        new Facet
                        {
                            Name = "Host",
                            DisplayName = "Hosts"
                        },
                        new Facet
                        {
                            Name = "MessageType",
                            DisplayName = "Message types"
                        }
                    })
                    .ConfigureAwait(false);

                var results = facetResults
                    .Results
                    .ToDictionary(
                        x => x.Key,
                        x => (object)x.Value
                        );

                Review.Assert("Check how to convert dictionary item VALUES, currently return object which must be typed");

                return results;
            }
        }

        public async Task<FailedMessage> ErrorBy(Guid failedMessageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var message = await session.LoadAsync<FailedMessage>(failedMessageId).ConfigureAwait(false);
                return message;
            }
        }

        public async Task<FailedMessageView> ErrorLastBy(Guid failedMessageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var message = await session.LoadAsync<FailedMessage>(failedMessageId).ConfigureAwait(false);
                var result = Map(message, session);
                return result;
            }
        }

        static FailedMessageView Map(FailedMessage message, IAsyncDocumentSession session)
        {
            var processingAttempt = message.ProcessingAttempts.Last();

            var metadata = processingAttempt.MessageMetadata;
            var failureDetails = processingAttempt.FailureDetails;
            var wasEdited = message.ProcessingAttempts.Last().Headers.ContainsKey("ServiceControl.EditOf");

            var failedMsgView = new FailedMessageView
            {
                Id = message.UniqueMessageId,
                MessageType = metadata.GetAsStringOrNull("MessageType"),
                IsSystemMessage = metadata.GetOrDefault<bool>("IsSystemMessage"),
                TimeSent = metadata.GetAsNullableDatetime("TimeSent"),
                MessageId = metadata.GetAsStringOrNull("MessageId"),
                Exception = failureDetails.Exception,
                QueueAddress = failureDetails.AddressOfFailingEndpoint,
                NumberOfProcessingAttempts = message.ProcessingAttempts.Count,
                Status = message.Status,
                TimeOfFailure = failureDetails.TimeOfFailure,
                LastModified = session.Advanced.GetMetadataFor(message)["Last-Modified"].Value<DateTime>(),
                Edited = wasEdited,
                EditOf = wasEdited ? message.ProcessingAttempts.Last().Headers["ServiceControl.EditOf"] : ""
            };

            try
            {
                failedMsgView.SendingEndpoint = metadata.GetOrDefault<EndpointDetails>("SendingEndpoint");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Unable to parse SendingEndpoint from metadata for messageId {message.UniqueMessageId}", ex);
                failedMsgView.SendingEndpoint = EndpointDetailsParser.SendingEndpoint(processingAttempt.Headers);
            }

            try
            {
                failedMsgView.ReceivingEndpoint = metadata.GetOrDefault<EndpointDetails>("ReceivingEndpoint");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Unable to parse ReceivingEndpoint from metadata for messageId {message.UniqueMessageId}", ex);
                failedMsgView.ReceivingEndpoint = EndpointDetailsParser.ReceivingEndpoint(processingAttempt.Headers);
            }

            return failedMsgView;
        }

        static readonly ILog Logger = LogManager.GetLogger<ErrorMessagesDataStore>();
    }
}