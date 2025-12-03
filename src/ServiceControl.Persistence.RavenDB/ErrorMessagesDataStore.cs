namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Editing;
    using Microsoft.Extensions.Logging;
    using Raven.Client;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands;
    using Raven.Client.Documents.Linq;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Documents.Queries;
    using Raven.Client.Documents.Queries.Facets;
    using Raven.Client.Documents.Session;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.EventLog;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Operations;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Recoverability;

    class ErrorMessagesDataStore(
        IRavenSessionProvider sessionProvider,
        IRavenDocumentStoreProvider documentStoreProvider,
        IBodyStorage bodyStorage,
        ExpirationManager expirationManager,
        ILogger<ErrorMessagesDataStore> logger)
        : IErrorMessageDataStore
    {
        public async Task<QueryResult<IList<MessagesView>>> GetAllMessages(
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            bool includeSystemMessages,
            DateTimeRange timeSentRange
            )
        {
            using var session = await sessionProvider.OpenSession();
            var query = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                .IncludeSystemMessagesWhere(includeSystemMessages)
                .FilterBySentTimeRange(timeSentRange)
                .Statistics(out var stats)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .OfType<FailedMessage>()
                .TransformToMessageView();

            var results = await query.ToListAsync();

            return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
        }

        public async Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(
            string endpointName,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            bool includeSystemMessages,
            DateTimeRange timeSentRange
            )
        {
            using var session = await sessionProvider.OpenSession();
            var query = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                .IncludeSystemMessagesWhere(includeSystemMessages)
                .FilterBySentTimeRange(timeSentRange)
                .Where(m => m.ReceivingEndpointName == endpointName)
                .Statistics(out var stats)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .OfType<FailedMessage>()
                .TransformToMessageView();

            var results = await query.ToListAsync();


            return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
        }

        public async Task<QueryResult<IList<MessagesView>>> SearchEndpointMessages(
            string endpointName,
            string searchKeyword,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            DateTimeRange timeSentRange
            )
        {
            using var session = await sessionProvider.OpenSession();
            var query = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                .Statistics(out var stats)
                .Search(x => x.Query, searchKeyword)
                .Where(m => m.ReceivingEndpointName == endpointName)
                .FilterBySentTimeRange(timeSentRange)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .OfType<FailedMessage>()
                .TransformToMessageView();

            var results = await query.ToListAsync();

            return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
        }

        public async Task<QueryResult<IList<MessagesView>>> GetAllMessagesByConversation(
            string conversationId,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            bool includeSystemMessages
            )
        {
            using var session = await sessionProvider.OpenSession();
            var query = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                .Statistics(out var stats)
                .Where(m => m.ConversationId == conversationId)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .OfType<FailedMessage>()
                .TransformToMessageView();

            var results = await query.ToListAsync();

            return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
        }

        public async Task<QueryResult<IList<MessagesView>>> GetAllMessagesForSearch(
            string searchTerms,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            DateTimeRange timeSentRange
            )
        {
            using var session = await sessionProvider.OpenSession();
            var query = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                .Statistics(out var stats)
                .Search(x => x.Query, searchTerms)
                .FilterBySentTimeRange(timeSentRange)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .OfType<FailedMessage>()
                .TransformToMessageView();

            var results = await query.ToListAsync();

            return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
        }

        public async Task FailedMessageMarkAsArchived(string failedMessageId)
        {
            using var session = await sessionProvider.OpenSession();
            var failedMessage = await session.LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(failedMessageId));

            if (failedMessage.Status != FailedMessageStatus.Archived)
            {
                failedMessage.Status = FailedMessageStatus.Archived;

                expirationManager.EnableExpiration(session, failedMessage);
            }

            await session.SaveChangesAsync();
        }

        public async Task<FailedMessage[]> FailedMessagesFetch(Guid[] ids)
        {
            using var session = await sessionProvider.OpenSession();
            var docIds = ids.Select(g => FailedMessageIdGenerator.MakeDocumentId(g.ToString()));
            var results = await session.LoadAsync<FailedMessage>(docIds);
            return results.Values.Where(x => x != null).ToArray();
        }

        public async Task StoreFailedErrorImport(FailedErrorImport failure)
        {
            using var session = await sessionProvider.OpenSession();
            await session.StoreAsync(failure);

            await session.SaveChangesAsync();
        }

        // the edit failed message manager manages the lifetime of the session
        public async Task<IEditFailedMessagesManager> CreateEditFailedMessageManager() =>
            new EditFailedMessageManager(await sessionProvider.OpenSession(), expirationManager);

        public async Task<QueryResult<FailureGroupView>> GetFailureGroupView(string groupId, string status, string modified)
        {
            using var session = await sessionProvider.OpenSession();
            var document = await session.Advanced
                .AsyncDocumentQuery<FailureGroupView, ArchivedGroupsViewIndex>()
                .Statistics(out var stats)
                .WhereEquals(group => group.Id, groupId)
                .FilterByStatusWhere(status)
                .FilterByLastModifiedRange(modified)
                .FirstOrDefaultAsync();

            return new QueryResult<FailureGroupView>(document, stats.ToQueryStatsInfo());
        }

        public async Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier)
        {
            using var session = await sessionProvider.OpenSession();
            var groups = session
                .Query<FailureGroupView, ArchivedGroupsViewIndex>()
                .Where(v => v.Type == classifier);

            var results = await groups
                .OrderByDescending(x => x.Last)
                .Take(200) // only show 200 groups
                .ToListAsync();

            return results;
        }

        public async Task<QueryResult<IList<FailedMessageView>>> ErrorGet(
            string status,
            string modified,
            string queueAddress,
            PagingInfo pagingInfo,
            SortInfo sortInfo
            )
        {
            using var session = await sessionProvider.OpenSession();
            var query = session.Advanced
                .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                .Statistics(out var stats)
                .FilterByStatusWhere(status)
                .FilterByLastModifiedRange(modified)
                .FilterByQueueAddress(queueAddress)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .SelectFields<FailedMessage>()
                .ToQueryable()
                .TransformToFailedMessageView();

            var results = await query
                .ToListAsync();

            return new QueryResult<IList<FailedMessageView>>(results, stats.ToQueryStatsInfo());
        }

        public async Task<QueryStatsInfo> ErrorsHead(
            string status,
            string modified,
            string queueAddress
            )
        {
            using var session = await sessionProvider.OpenSession();
            var stats = await session.Advanced
                .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                .FilterByStatusWhere(status)
                .FilterByLastModifiedRange(modified)
                .FilterByQueueAddress(queueAddress)
                .GetQueryResultAsync();

            return stats.ToQueryStatsInfo();
        }

        public async Task<QueryResult<IList<FailedMessageView>>> ErrorsByEndpointName(
            string status,
            string endpointName,
            string modified,
            PagingInfo pagingInfo,
            SortInfo sortInfo
            )
        {
            using var session = await sessionProvider.OpenSession();
            var query = session.Advanced
                .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                .Statistics(out var stats)
                .FilterByStatusWhere(status)
                .AndAlso()
                .WhereEquals("ReceivingEndpointName", endpointName)
                .FilterByLastModifiedRange(modified)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .SelectFields<FailedMessage>()
                .ToQueryable()
                .TransformToFailedMessageView();

            var results = await query
                .ToListAsync();

            return new QueryResult<IList<FailedMessageView>>(results, stats.ToQueryStatsInfo());
        }

        public async Task<IDictionary<string, object>> ErrorsSummary()
        {
            using var session = await sessionProvider.OpenSession();
            var facetResults = await session.Query<FailedMessage, FailedMessageFacetsIndex>()
                .AggregateBy(new List<Facet>
                {
                    new Facet
                    {
                        FieldName = "Name",
                        DisplayFieldName = "Endpoints"
                    },
                    new Facet
                    {
                        FieldName = "Host",
                        DisplayFieldName = "Hosts"
                    },
                    new Facet
                    {
                        FieldName = "MessageType",
                        DisplayFieldName = "Message types"
                    }
                }).ExecuteAsync();

            var results = facetResults
                .ToDictionary(
                    x => x.Key,
                    x => (object)x.Value
                );

            return results;
        }

        public Task<FailedMessage> ErrorBy(string failedMessageId) => ErrorByDocumentId(FailedMessageIdGenerator.MakeDocumentId(failedMessageId));

        async Task<FailedMessage> ErrorByDocumentId(string documentId)
        {
            using var session = await sessionProvider.OpenSession();
            var message = await session.LoadAsync<FailedMessage>(documentId);
            return message;
        }

        // the notifications manager manages the lifetime of the session
        public async Task<INotificationsManager> CreateNotificationsManager() =>
            new NotificationsManager(await sessionProvider.OpenSession());

        public async Task<FailedMessageView> ErrorLastBy(string failedMessageId)
        {
            using var session = await sessionProvider.OpenSession();
            var message = await session.LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(failedMessageId));
            if (message == null)
            {
                return null;
            }
            var result = Map(message, session);
            return result;
        }

        FailedMessageView Map(FailedMessage message, IAsyncDocumentSession session)
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
                TimeSent = metadata.GetAsNullableDateTime("TimeSent"),
                MessageId = metadata.GetAsStringOrNull("MessageId"),
                Exception = failureDetails.Exception,
                QueueAddress = failureDetails.AddressOfFailingEndpoint,
                NumberOfProcessingAttempts = message.ProcessingAttempts.Count,
                Status = message.Status,
                TimeOfFailure = failureDetails.TimeOfFailure,
                LastModified = session.Advanced.GetLastModifiedFor(message).Value,
                Edited = wasEdited,
                EditOf = wasEdited ? message.ProcessingAttempts.Last().Headers["ServiceControl.EditOf"] : ""
            };

            try
            {
                failedMsgView.SendingEndpoint = metadata.GetOrDefault<EndpointDetails>("SendingEndpoint");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unable to parse SendingEndpoint from metadata for messageId {UniqueMessageId}", message.UniqueMessageId);
                failedMsgView.SendingEndpoint = EndpointDetailsParser.SendingEndpoint(processingAttempt.Headers);
            }

            try
            {
                failedMsgView.ReceivingEndpoint = metadata.GetOrDefault<EndpointDetails>("ReceivingEndpoint");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unable to parse ReceivingEndpoint from metadata for messageId {UniqueMessageId}", message.UniqueMessageId);
                failedMsgView.ReceivingEndpoint = EndpointDetailsParser.ReceivingEndpoint(processingAttempt.Headers);
            }

            return failedMsgView;
        }


        public async Task EditComment(string groupId, string comment)
        {
            using var session = await sessionProvider.OpenSession();
            var groupComment =
                await session.LoadAsync<GroupComment>(GroupComment.MakeId(groupId))
                ?? new GroupComment { Id = GroupComment.MakeId(groupId) };

            groupComment.Comment = comment;

            await session.StoreAsync(groupComment);
            await session.SaveChangesAsync();
        }

        public async Task DeleteComment(string groupId)
        {
            using var session = await sessionProvider.OpenSession();
            session.Delete(GroupComment.MakeId(groupId));
            await session.SaveChangesAsync();
        }

        public async Task<QueryResult<IList<FailedMessageView>>> GetGroupErrors(
            string groupId,
            string status,
            string modified,
            SortInfo sortInfo,
            PagingInfo pagingInfo
            )
        {
            using var session = await sessionProvider.OpenSession();
            var query = session.Advanced
                .AsyncDocumentQuery<FailureGroupMessageView, FailedMessages_ByGroup>()
                .Statistics(out var stats)
                .WhereEquals(view => view.FailureGroupId, groupId)
                .FilterByStatusWhere(status)
                .FilterByLastModifiedRange(modified)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .SelectFields<FailedMessage>()
                .ToQueryable()
                .TransformToFailedMessageView();

            var results = await query
                .ToListAsync();

            return results.ToQueryResult(stats);
        }

        public async Task<QueryStatsInfo> GetGroupErrorsCount(string groupId, string status, string modified)
        {
            using var session = await sessionProvider.OpenSession();
            var queryResult = await session.Advanced
                .AsyncDocumentQuery<FailureGroupMessageView, FailedMessages_ByGroup>()
                .WhereEquals(view => view.FailureGroupId, groupId)
                .FilterByStatusWhere(status)
                .FilterByLastModifiedRange(modified)
                .GetQueryResultAsync();

            return queryResult.ToQueryStatsInfo();
        }

        public async Task<QueryResult<IList<FailureGroupView>>> GetGroup(string groupId, string status, string modified)
        {
            using var session = await sessionProvider.OpenSession();
            var queryResult = await session.Advanced
                .AsyncDocumentQuery<FailureGroupView, FailureGroupsViewIndex>()
                .Statistics(out var stats)
                .WhereEquals(group => group.Id, groupId)
                .FilterByStatusWhere(status)
                .FilterByLastModifiedRange(modified)
                .ToListAsync();

            return queryResult.ToQueryResult(stats);
        }

        public async Task<bool> MarkMessageAsResolved(string failedMessageId)
        {
            var documentId = FailedMessageIdGenerator.MakeDocumentId(failedMessageId);

            using var session = await sessionProvider.OpenSession();
            session.Advanced.UseOptimisticConcurrency = true;

            var failedMessage = await session.LoadAsync<FailedMessage>(documentId);

            if (failedMessage == null)
            {
                return false;
            }

            failedMessage.Status = FailedMessageStatus.Resolved;

            expirationManager.EnableExpiration(session, failedMessage);

            await session.SaveChangesAsync();

            return true;
        }

        public async Task ProcessPendingRetries(DateTime periodFrom, DateTime periodTo, string queueAddress, Func<string, Task> processCallback)
        {
            using var session = await sessionProvider.OpenSession();
            var prequery = session.Advanced
                .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                .WhereEquals("Status", (int)FailedMessageStatus.RetryIssued)
                .AndAlso()
                .WhereBetween("LastModified", periodFrom.Ticks, periodTo.Ticks);

            if (!string.IsNullOrWhiteSpace(queueAddress))
            {
                prequery = prequery.AndAlso()
                    .WhereEquals(options => options.QueueAddress, queueAddress);
            }

            var query = prequery
                .SelectFields<FailedMessage>()
                .ToQueryable()
                .TransformToFailedMessageView();

            await using var ie = await session.Advanced.StreamAsync(query);
            while (await ie.MoveNextAsync())
            {
                await processCallback(ie.Current.Document.Id);
            }
        }

        class DocumentPatchResult
        {
            public string Document { get; set; }
        }

        public async Task<string[]> UnArchiveMessagesByRange(DateTime from, DateTime to)
        {
            const int Unresolved = (int)FailedMessageStatus.Unresolved;
            const int Archived = (int)FailedMessageStatus.Archived;

            var indexName = new FailedMessageViewIndex().IndexName;
            var query = new IndexQuery
            {
                // Set based args are treated differently ($name) than other places (args.name)!
                // https://ravendb.net/docs/article-page/5.4/csharp/client-api/operations/patching/set-based
                // Removing a property in a patch
                // https://ravendb.net/docs/article-page/5.4/Csharp/client-api/operations/patching/single-document#remove-property
                Query = $@"from index '{indexName}' as msg
                           where msg.Status == {Archived} and msg.LastModified >= $from and msg.LastModified <= $to
                           update
                           {{
                                msg.Status = {Unresolved};
                                {ExpirationManager.DeleteExpirationFieldExpression};
                           }}",
                QueryParameters = new Parameters
                {
                    { "from", from.Ticks },
                    { "to", to.Ticks }
                }
            };

            var patch = new PatchByQueryOperation(query, new QueryOperationOptions
            {
                AllowStale = true,
                RetrieveDetails = true
            });

            var documentStore = await documentStoreProvider.GetDocumentStore();
            var operation = await documentStore.Operations.SendAsync(patch);

            var result = await operation.WaitForCompletionAsync<BulkOperationResult>();

            var ids = result.Details.OfType<BulkOperationResult.PatchDetails>()
                .Select(d => d.Id)
                .ToArray();

            return ids;
        }

        public async Task<string[]> UnArchiveMessages(IEnumerable<string> failedMessageIds)
        {
            Dictionary<string, FailedMessage> failedMessages;

            using var session = await sessionProvider.OpenSession();
            session.Advanced.UseOptimisticConcurrency = true;

            var documentIds = failedMessageIds.Select(FailedMessageIdGenerator.MakeDocumentId);

            failedMessages = await session.LoadAsync<FailedMessage>(documentIds);

            foreach (var failedMessage in failedMessages.Values)
            {
                if (failedMessage.Status == FailedMessageStatus.Archived)
                {
                    failedMessage.Status = FailedMessageStatus.Unresolved;
                    session.Advanced.GetMetadataFor(failedMessage).Remove(Constants.Documents.Metadata.Expires);
                }
            }

            await session.SaveChangesAsync();

            // Return the unique IDs - the dictionary keys are document ids with a prefix
            return failedMessages.Values.Select(x => x.UniqueMessageId).ToArray();
        }

        public async Task RevertRetry(string messageUniqueId)
        {
            using var session = await sessionProvider.OpenSession();
            var failedMessage = await session
                .LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(messageUniqueId));
            failedMessage?.Status = FailedMessageStatus.Unresolved;

            var failedMessageRetry = await session
                .LoadAsync<FailedMessageRetry>(FailedMessageRetry.MakeDocumentId(messageUniqueId));
            if (failedMessageRetry != null)
            {
                session.Delete(failedMessageRetry);
            }

            await session.SaveChangesAsync();
        }

        public async Task RemoveFailedMessageRetryDocument(string uniqueMessageId)
        {
            using var session = await sessionProvider.OpenSession();
            await session.Advanced.RequestExecutor.ExecuteAsync(new DeleteDocumentCommand(FailedMessageRetry.MakeDocumentId(uniqueMessageId), null), session.Advanced.Context);
        }

        public async Task<string[]> GetRetryPendingMessages(DateTime from, DateTime to, string queueAddress)
        {
            using var session = await sessionProvider.OpenSession();
            var query = session
                .Query<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                .Where(o => o.Status == FailedMessageStatus.RetryIssued && o.LastModified >= from.Ticks && o.LastModified <= to.Ticks && o.QueueAddress == queueAddress)
                .OfType<FailedMessageProjection>();

            int index = 0;
            await using var streamResults = await session.Advanced.StreamAsync(query, out var streamQueryStatistics);
            string[] ids = new string[streamQueryStatistics.TotalResults];
            while (await streamResults.MoveNextAsync())
            {
                ids[index++] = streamResults.Current.Document.UniqueMessageId;
            }
            return ids;
        }

        record struct FailedMessageProjection(string UniqueMessageId);

        public async Task<byte[]> FetchFromFailedMessage(string uniqueMessageId)
        {
            byte[] body = null;
            var result = await bodyStorage.TryFetch(uniqueMessageId)
                         ?? throw new InvalidOperationException("IBodyStorage.TryFetch result cannot be null");

            if (result.HasResult)
            {
                await using (result.Stream) // Not strictly required for MemoryStream but might be different behavior in future .NET versions
                {
                    // Unfortunately we can't use the buffer manager here yet because core doesn't allow to set the length property so usage of GetBuffer is not possible
                    // furthermore call ToArray would neglect many of the benefits of the recyclable stream
                    // RavenDB always returns a memory stream in ver. 3.5 so there is no need to pretend we need to do buffered reads since the memory is anyway fully allocated already
                    // this assumption might change when we stop supporting RavenDB 3.5 but right now this is the most memory efficient way to do things
                    // https://github.com/microsoft/Microsoft.IO.RecyclableMemoryStream#getbuffer-and-toarray
                    using var memoryStream = new MemoryStream();
                    await result.Stream.CopyToAsync(memoryStream);

                    body = memoryStream.ToArray();
                }
            }
            return body;
        }

        public async Task StoreEventLogItem(EventLogItem logItem)
        {
            using var session = await sessionProvider.OpenSession();
            await session.StoreAsync(logItem);

            expirationManager.EnableExpiration(session, logItem);

            await session.SaveChangesAsync();
        }

        public async Task StoreFailedMessagesForTestsOnly(params FailedMessage[] failedMessages)
        {
            using var session = await sessionProvider.OpenSession();
            foreach (var message in failedMessages)
            {
                await session.StoreAsync(message);
            }

            await session.SaveChangesAsync();
        }
    }
}
