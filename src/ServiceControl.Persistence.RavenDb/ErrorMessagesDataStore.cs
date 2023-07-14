﻿namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Editing;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Operations;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Recoverability;

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

        public async Task<FailedMessage> ErrorBy(string failedMessageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var message = await session.LoadAsync<FailedMessage>(failedMessageId).ConfigureAwait(false);
                return message;
            }
        }

        public Task<INotificationsManager> CreateNotificationsManager()
        {
            var session = documentStore.OpenAsyncSession();
            var manager = new NotificationsManager(session);

            return Task.FromResult<INotificationsManager>(manager);
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


        public async Task EditComment(string groupId, string comment)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var groupComment =
                    await session.LoadAsync<GroupComment>(GroupComment.MakeId(groupId)).ConfigureAwait(false)
                    ?? new GroupComment { Id = GroupComment.MakeId(groupId) };

                groupComment.Comment = comment;

                await session.StoreAsync(groupComment).ConfigureAwait(false);
                await session.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task DeleteComment(string groupId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                session.Delete(GroupComment.MakeId(groupId));
                await session.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task<QueryResult<IList<FailedMessageView>>> GetGroupErrors(
            string groupId,
            string status,
            string modified,
            SortInfo sortInfo,
            PagingInfo pagingInfo
            )
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Advanced
                    .AsyncDocumentQuery<FailureGroupMessageView, FailedMessages_ByGroup>()
                    .Statistics(out var stats)
                    .WhereEquals(view => view.FailureGroupId, groupId)
                    .FilterByStatusWhere(status)
                    .FilterByLastModifiedRange(modified)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .SetResultTransformer(FailedMessageViewTransformer.Name)
                    .SelectFields<FailedMessageView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return results.ToQueryResult(stats);
            }
        }

        public async Task<QueryStatsInfo> GetGroupErrorsCount(string groupId, string status, string modified)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var queryResult = await session.Advanced
                    .AsyncDocumentQuery<FailureGroupMessageView, FailedMessages_ByGroup>()
                    .WhereEquals(view => view.FailureGroupId, groupId)
                    .FilterByStatusWhere(status)
                    .FilterByLastModifiedRange(modified)
                    .QueryResultAsync()
                    .ConfigureAwait(false);

                return queryResult.ToQueryStatsInfo();
            }
        }

        public async Task<RetryHistory> GetRetryHistory()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var id = RetryHistory.MakeId();
                var retryHistory = await session.LoadAsync<RetryHistory>(id)
                    .ConfigureAwait(false);

                retryHistory = retryHistory ?? RetryHistory.CreateNew();

                return retryHistory;
            }
        }

        public async Task<QueryResult<IList<FailureGroupView>>> GetGroup(string groupId, string status, string modified)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var queryResult = await session.Advanced
                    .AsyncDocumentQuery<FailureGroupView, FailureGroupsViewIndex>()
                    .Statistics(out var stats)
                    .WhereEquals(group => group.Id, groupId)
                    .FilterByStatusWhere(status)
                    .FilterByLastModifiedRange(modified)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return queryResult.ToQueryResult(stats);
            }
        }

        public async Task<bool> MarkMessageAsResolved(string failedMessageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var failedMessage = await session.LoadAsync<FailedMessage>(new Guid(failedMessageId))
                    .ConfigureAwait(false);

                if (failedMessage == null)
                {
                    return false;
                }

                failedMessage.Status = FailedMessageStatus.Resolved;

                await session.SaveChangesAsync().ConfigureAwait(false);

                return true;
            }
        }

        public async Task ProcessPendingRetries(DateTime periodFrom, DateTime periodTo, string queueAddress, Func<string, Task> processCallback)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var prequery = session.Advanced
                    .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                .WhereEquals("Status", (int)FailedMessageStatus.RetryIssued)
                .AndAlso()
                .WhereBetweenOrEqual("LastModified", periodFrom.Ticks, periodTo.Ticks);

                if (!string.IsNullOrWhiteSpace(queueAddress))
                {
                    prequery = prequery.AndAlso()
                        .WhereEquals(options => options.QueueAddress, queueAddress);
                }

                var query = prequery
                    .SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                    .SelectFields<FailedMessageView>();

                using (var ie = await session.Advanced.StreamAsync(query).ConfigureAwait(false))
                {
                    while (await ie.MoveNextAsync().ConfigureAwait(false))
                    {
                        await processCallback(ie.Current.Document.Id)
                            .ConfigureAwait(false);
                    }
                }
            }
        }

        class DocumentPatchResult
        {
            public string Document { get; set; }
        }

        public async Task<(string[] ids, int count)> UnArchiveMessagesByRange(DateTime from, DateTime to, DateTime cutOff)
        {
            var options = new BulkOperationOptions
            {
                AllowStale = true
            };

            var result = await documentStore.AsyncDatabaseCommands.UpdateByIndexAsync(
                new FailedMessageViewIndex().IndexName,
                new IndexQuery
                {
                    Query = string.Format(CultureInfo.InvariantCulture, "LastModified:[{0} TO {1}] AND Status:{2}", from.Ticks, to.Ticks, (int)FailedMessageStatus.Archived),
                    Cutoff = cutOff
                }, new ScriptedPatchRequest
                {
                    Script = @"
if(this.Status === archivedStatus) {
    this.Status = unresolvedStatus;
}
",
                    Values =
                    {
                        {"archivedStatus", (int)FailedMessageStatus.Archived},
                        {"unresolvedStatus", (int)FailedMessageStatus.Unresolved}
                    }
                }, options).ConfigureAwait(false);

            var patchedDocumentIds = (await result.WaitForCompletionAsync().ConfigureAwait(false))
                .JsonDeserialization<DocumentPatchResult[]>();

            return (
                patchedDocumentIds.Select(x => FailedMessage.GetMessageIdFromDocumentId(x.Document)).ToArray(),
                patchedDocumentIds.Length
                );
        }

        public async Task<(string[] ids, int count)> UnArchiveMessages(IEnumerable<string> failedMessageIds)
        {
            FailedMessage[] failedMessages;

            using (var session = documentStore.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var documentIds = failedMessageIds.Select(FailedMessage.MakeDocumentId);

                failedMessages = await session.LoadAsync<FailedMessage>(documentIds)
                    .ConfigureAwait(false);

                foreach (var failedMessage in failedMessages)
                {
                    if (failedMessage.Status == FailedMessageStatus.Archived)
                    {
                        failedMessage.Status = FailedMessageStatus.Unresolved;
                    }
                }

                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            return (
                failedMessages.Select(x => x.UniqueMessageId).ToArray(),
                failedMessages.Length
                );
        }

        public async Task RecordRetryOperationCompleted(string requestId, RetryType retryType, DateTime startTime, DateTime completionTime,
            string originator, string classifier, bool messageFailed, int numberOfMessagesProcessed, DateTime lastProcessed, int retryHistoryDepth)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var retryHistory = await session.LoadAsync<RetryHistory>(RetryHistory.MakeId()).ConfigureAwait(false) ??
                                   RetryHistory.CreateNew();

                retryHistory.AddToUnacknowledged(new UnacknowledgedRetryOperation
                {
                    RequestId = requestId,
                    RetryType = retryType,
                    StartTime = startTime,
                    CompletionTime = completionTime,
                    Originator = originator,
                    Classifier = classifier,
                    Failed = messageFailed,
                    NumberOfMessagesProcessed = numberOfMessagesProcessed,
                    Last = lastProcessed
                });

                retryHistory.AddToHistory(new HistoricRetryOperation
                {
                    RequestId = requestId,
                    RetryType = retryType,
                    StartTime = startTime,
                    CompletionTime = completionTime,
                    Originator = originator,
                    Failed = messageFailed,
                    NumberOfMessagesProcessed = numberOfMessagesProcessed
                }, retryHistoryDepth);

                await session.StoreAsync(retryHistory)
                    .ConfigureAwait(false);
                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
        }

        static readonly ILog Logger = LogManager.GetLogger<ErrorMessagesDataStore>();
    }
}