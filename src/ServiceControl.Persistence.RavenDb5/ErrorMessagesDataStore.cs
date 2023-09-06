namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Editing;
    using NServiceBus.Logging;
    using ServiceControl.EventLog;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Operations;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Recoverability;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Queries;
    using Raven.Client.Documents.Session;
    using Raven.Client.Linq;
    using Raven.Json.Linq;

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
                    .ToListAsync();

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
                    .ToListAsync();

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> SearchEndpointMessages(
            string endpointName,
            string searchKeyword,
            PagingInfo pagingInfo,
            SortInfo sortInfo
            )
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Search(x => x.Query, searchKeyword)
                    .Where(m => m.ReceivingEndpointName == endpointName)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync();

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
                    .ToListAsync();

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
                    .ToListAsync();

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
                    .ToListAsync();

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<FailedMessage> FailedMessageFetch(string failedMessageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                return await session.LoadAsync<FailedMessage>(new Guid(failedMessageId));
            }
        }

        public async Task FailedMessageMarkAsArchived(string failedMessageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var failedMessage = await session.LoadAsync<FailedMessage>(new Guid(failedMessageId));

                if (failedMessage.Status != FailedMessageStatus.Archived)
                {
                    failedMessage.Status = FailedMessageStatus.Archived;
                }

                await session.SaveChangesAsync();
            }
        }

        public async Task<FailedMessage[]> FailedMessagesFetch(Guid[] ids)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.LoadAsync<FailedMessage>(ids.Cast<ValueType>());
                return results.Where(x => x != null).ToArray();
            }
        }

        public async Task StoreFailedErrorImport(FailedErrorImport failure)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                await session.StoreAsync(failure);

                await session.SaveChangesAsync();
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
                    .FirstOrDefaultAsync();

                return new QueryResult<FailureGroupView>(document, stats.ToQueryStatsInfo());
            }
        }

        public async Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var groups = session
                    .Query<FailureGroupView, ArchivedGroupsViewIndex>()
                    .Where(v => v.Type == classifier);

                var results = await groups
                    .OrderByDescending(x => x.Last)
                    .Take(200) // only show 200 groups
                    .ToListAsync();

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
                    .ToListAsync();

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
                    .QueryResultAsync();

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
                    .ToListAsync();

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
                    });

                var results = facetResults
                    .Results
                    .ToDictionary(
                        x => x.Key,
                        x => (object)x.Value
                        );

                return results;
            }
        }

        public async Task<FailedMessage> ErrorBy(Guid failedMessageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var message = await session.LoadAsync<FailedMessage>(failedMessageId);
                return message;
            }
        }

        public async Task<FailedMessage> ErrorBy(string failedMessageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var message = await session.LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(failedMessageId));
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
                var message = await session.LoadAsync<FailedMessage>(failedMessageId);
                if (message == null)
                {
                    return null;
                }
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
                    await session.LoadAsync<GroupComment>(GroupComment.MakeId(groupId))
                    ?? new GroupComment { Id = GroupComment.MakeId(groupId) };

                groupComment.Comment = comment;

                await session.StoreAsync(groupComment);
                await session.SaveChangesAsync();
            }
        }

        public async Task DeleteComment(string groupId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                session.Delete(GroupComment.MakeId(groupId));
                await session.SaveChangesAsync();
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
                    .ToListAsync();

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
                    .QueryResultAsync();

                return queryResult.ToQueryStatsInfo();
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
                    .ToListAsync();

                return queryResult.ToQueryResult(stats);
            }
        }

        public async Task<bool> MarkMessageAsResolved(string failedMessageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var failedMessage = await session.LoadAsync<FailedMessage>(new Guid(failedMessageId));

                if (failedMessage == null)
                {
                    return false;
                }

                failedMessage.Status = FailedMessageStatus.Resolved;

                await session.SaveChangesAsync();

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

                using (var ie = await session.Advanced.StreamAsync(query))
                {
                    while (await ie.MoveNextAsync())
                    {
                        await processCallback(ie.Current.Document.Id);
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
                }, options);

            var patchedDocumentIds = (await result.WaitForCompletionAsync())
                .JsonDeserialization<DocumentPatchResult[]>();

            return (
                patchedDocumentIds.Select(x => FailedMessageIdGenerator.GetMessageIdFromDocumentId(x.Document)).ToArray(),
                patchedDocumentIds.Length
                );
        }

        public async Task<(string[] ids, int count)> UnArchiveMessages(IEnumerable<string> failedMessageIds)
        {
            FailedMessage[] failedMessages;

            using (var session = documentStore.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var documentIds = failedMessageIds.Select(FailedMessageIdGenerator.MakeDocumentId);

                failedMessages = await session.LoadAsync<FailedMessage>(documentIds);

                foreach (var failedMessage in failedMessages)
                {
                    if (failedMessage.Status == FailedMessageStatus.Archived)
                    {
                        failedMessage.Status = FailedMessageStatus.Unresolved;
                    }
                }

                await session.SaveChangesAsync();
            }

            return (
                failedMessages.Select(x => x.UniqueMessageId).ToArray(),
                failedMessages.Length
                );
        }

        public async Task RevertRetry(string messageUniqueId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var failedMessage = await session
                    .LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(messageUniqueId));
                if (failedMessage != null)
                {
                    failedMessage.Status = FailedMessageStatus.Unresolved;
                }

                var failedMessageRetry = await session
                    .LoadAsync<FailedMessageRetry>(FailedMessageRetry.MakeDocumentId(messageUniqueId));
                if (failedMessageRetry != null)
                {
                    session.Delete(failedMessageRetry);
                }

                await session.SaveChangesAsync();
            }
        }

        public Task RemoveFailedMessageRetryDocument(string uniqueMessageId)
        {
            return documentStore.AsyncDatabaseCommands.DeleteAsync(FailedMessageRetry.MakeDocumentId(uniqueMessageId), null);
        }

        // TODO: Once using .NET, consider using IAsyncEnumerable here as this is an unbounded query
        public async Task<string[]> GetRetryPendingMessages(DateTime from, DateTime to, string queueAddress)
        {
            var ids = new List<string>();

            using (var session = documentStore.OpenAsyncSession())
            {
                var query = session.Advanced
                    .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .WhereEquals("Status", (int)FailedMessageStatus.RetryIssued)
                .AndAlso()
                .WhereBetweenOrEqual(options => options.LastModified, from.Ticks, to.Ticks)
                .AndAlso()
                    .WhereEquals(o => o.QueueAddress, queueAddress)
                    .SetResultTransformer(FailedMessageViewTransformer.Name)
                    .SelectFields<FailedMessageView>(new[] { "Id" });

                using (var ie = await session.Advanced.StreamAsync(query))
                {
                    while (await ie.MoveNextAsync())
                    {
                        ids.Add(ie.Current.Document.Id);
                    }
                }
            }

            return ids.ToArray();
        }

        public async Task<byte[]> FetchFromFailedMessage(string uniqueMessageId)
        {
            string documentId = FailedMessageIdGenerator.MakeDocumentId(uniqueMessageId);
            var results = await documentStore.AsyncDatabaseCommands.GetAsync(new[] { documentId }, null,
                transformer: MessagesBodyTransformer.Name);

            string resultBody = ((results.Results?.SingleOrDefault()?["$values"] as RavenJArray)?.SingleOrDefault() as RavenJObject)
                ?.ToObject<MessagesBodyTransformer.Result>()?.Body;

            if (resultBody != null)
            {
                return Encoding.UTF8.GetBytes(resultBody);
            }

            return null;
        }

        public async Task StoreEventLogItem(EventLogItem logItem)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                await session.StoreAsync(logItem);
                await session.SaveChangesAsync();
            }
        }

        public async Task StoreFailedMessagesForTestsOnly(params FailedMessage[] failedMessages)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                foreach (var message in failedMessages)
                {
                    await session.StoreAsync(message);
                }

                await session.SaveChangesAsync();
            }
        }

        static readonly ILog Logger = LogManager.GetLogger<ErrorMessagesDataStore>();
    }
}
