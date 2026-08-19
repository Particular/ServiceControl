namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
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
    using Recoverability;
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
        ExpirationManager expirationManager,
        ILogger<ErrorMessagesDataStore> logger)
        : IMessagesViewDataStore, IFailedMessageQueryDataStore, IFailedMessageLifecycleDataStore, IFailedMessageRetryDataStore
    {
        public async Task<QueryResult<IList<MessagesView>>> GetAllMessages(
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            bool includeSystemMessages,
            DateTimeRange timeSentRange,
            CancellationToken cancellationToken = default
            )
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var query = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                .IncludeSystemMessagesWhere(includeSystemMessages)
                .FilterBySentTimeRange(timeSentRange)
                .Statistics(out var stats)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .OfType<FailedMessage>()
                .TransformToMessageView();

            var results = await query.ToListAsync(cancellationToken);

            return new QueryResult<IList<MessagesView>>(results, stats.ToPagedQueryStatsInfo(results, view => view.Id));
        }

        public async Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(
            string endpointName,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            bool includeSystemMessages,
            DateTimeRange timeSentRange,
            CancellationToken cancellationToken = default
            )
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var query = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                .IncludeSystemMessagesWhere(includeSystemMessages)
                .FilterBySentTimeRange(timeSentRange)
                .Where(m => m.ReceivingEndpointName == endpointName)
                .Statistics(out var stats)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .OfType<FailedMessage>()
                .TransformToMessageView();

            var results = await query.ToListAsync(cancellationToken);


            return new QueryResult<IList<MessagesView>>(results, stats.ToPagedQueryStatsInfo(results, view => view.Id));
        }

        public async Task<QueryResult<IList<MessagesView>>> SearchEndpointMessages(
            string endpointName,
            string searchKeyword,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            DateTimeRange timeSentRange,
            CancellationToken cancellationToken = default
            )
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var query = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                .Statistics(out var stats)
                .Search(x => x.Query, searchKeyword)
                .Where(m => m.ReceivingEndpointName == endpointName)
                .FilterBySentTimeRange(timeSentRange)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .OfType<FailedMessage>()
                .TransformToMessageView();

            var results = await query.ToListAsync(cancellationToken);

            return new QueryResult<IList<MessagesView>>(results, stats.ToPagedQueryStatsInfo(results, view => view.Id));
        }

        public async Task<QueryResult<IList<MessagesView>>> GetAllMessagesByConversation(
            string conversationId,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            bool includeSystemMessages,
            CancellationToken cancellationToken = default
            )
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var query = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                .Statistics(out var stats)
                .Where(m => m.ConversationId == conversationId)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .OfType<FailedMessage>()
                .TransformToMessageView();

            var results = await query.ToListAsync(cancellationToken);

            return new QueryResult<IList<MessagesView>>(results, stats.ToPagedQueryStatsInfo(results, view => view.Id));
        }

        public async Task<QueryResult<IList<MessagesView>>> GetAllMessagesForSearch(
            string searchTerms,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            DateTimeRange timeSentRange,
            CancellationToken cancellationToken = default
            )
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var query = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                .Statistics(out var stats)
                .Search(x => x.Query, searchTerms)
                .FilterBySentTimeRange(timeSentRange)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .OfType<FailedMessage>()
                .TransformToMessageView();

            var results = await query.ToListAsync(cancellationToken);

            return new QueryResult<IList<MessagesView>>(results, stats.ToPagedQueryStatsInfo(results, view => view.Id));
        }

        public async Task MarkAsArchived(string failedMessageId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var failedMessage = await session.LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(failedMessageId), cancellationToken);

            if (failedMessage.Status != FailedMessageStatus.Archived)
            {
                failedMessage.Status = FailedMessageStatus.Archived;

                expirationManager.EnableExpiration(session, failedMessage);
            }

            await session.SaveChangesAsync(cancellationToken);
        }

        public async Task<FailedMessage[]> GetFailedMessagesByIds(Guid[] ids, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var docIds = ids.Select(g => FailedMessageIdGenerator.MakeDocumentId(g.ToString()));
            var results = await session.LoadAsync<FailedMessage>(docIds, cancellationToken);
            return results.Values.Where(x => x != null).ToArray();
        }

        public async Task<QueryResult<IList<FailedMessageView>>> GetFailedMessages(
            string status,
            string modified,
            string queueAddress,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            CancellationToken cancellationToken = default
            )
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
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
                .ToListAsync(cancellationToken);

            return new QueryResult<IList<FailedMessageView>>(results, stats.ToPagedQueryStatsInfo(results, view => view.Id));
        }

        public async Task<QueryStatsInfo> GetFailedMessagesStats(
            string status,
            string modified,
            string queueAddress,
            CancellationToken cancellationToken = default
            )
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var stats = await session.Advanced
                .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                .FilterByStatusWhere(status)
                .FilterByLastModifiedRange(modified)
                .FilterByQueueAddress(queueAddress)
                .GetQueryResultAsync(cancellationToken);

            return stats.ToQueryStatsInfo();
        }

        public async Task<QueryResult<IList<FailedMessageView>>> GetFailedMessagesByEndpoint(
            string status,
            string endpointName,
            string modified,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            CancellationToken cancellationToken = default
            )
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
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
                .ToListAsync(cancellationToken);

            return new QueryResult<IList<FailedMessageView>>(results, stats.ToPagedQueryStatsInfo(results, view => view.Id));
        }

        public async Task<IDictionary<string, object>> GetFailedMessagesSummary(CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            // The facets are named after the index fields and renamed afterwards. Setting
            // DisplayFieldName instead makes the server resolve the field by that name, which then
            // fails with "Field Endpoints not found in Index".
            var facetResults = await session.Query<FailedMessage, FailedMessageFacetsIndex>()
                .AggregateBy(new List<Facet>
                {
                    new Facet { FieldName = "Name" },
                    new Facet { FieldName = "Host" },
                    new Facet { FieldName = "MessageType" }
                }).ExecuteAsync(cancellationToken);

            return new Dictionary<string, object>
            {
                [FailedMessageSummaryKeys.Endpoints] = Counts(facetResults, "Name"),
                [FailedMessageSummaryKeys.Hosts] = Counts(facetResults, "Host"),
                [FailedMessageSummaryKeys.MessageTypes] = Counts(facetResults, "MessageType")
            };
        }

        static Dictionary<string, object> Counts(Dictionary<string, FacetResult> facetResults, string fieldName) =>
            facetResults[fieldName].Values.ToDictionary(value => value.Range, value => (object)value.Count);

        public async Task<FailedMessage> GetFailedMessage(string failedMessageId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var message = await session.LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(failedMessageId), cancellationToken);
            return message;
        }

        public async Task<FailedMessageView> GetLatestFailedMessageView(string failedMessageId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var message = await session.LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(failedMessageId), cancellationToken);
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


        public async Task<bool> MarkAsResolved(string failedMessageId, CancellationToken cancellationToken = default)
        {
            var documentId = FailedMessageIdGenerator.MakeDocumentId(failedMessageId);

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            session.Advanced.UseOptimisticConcurrency = true;

            var failedMessage = await session.LoadAsync<FailedMessage>(documentId, cancellationToken);

            if (failedMessage == null)
            {
                return false;
            }

            failedMessage.Status = FailedMessageStatus.Resolved;

            expirationManager.EnableExpiration(session, failedMessage);

            await session.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task ProcessPendingRetries(DateTime periodFrom, DateTime periodTo, string queueAddress, Func<string, CancellationToken, Task> processCallback, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
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

            await using var ie = await session.Advanced.StreamAsync(query, cancellationToken);
            while (await ie.MoveNextAsync())
            {
                await processCallback(ie.Current.Document.Id, cancellationToken);
            }
        }

        public async Task<string[]> UnArchiveMessagesByRange(DateTime from, DateTime to, CancellationToken cancellationToken = default)
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

            var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
            var operation = await documentStore.Operations.SendAsync(patch, token: cancellationToken);

            var result = await operation.WaitForCompletionAsync<BulkOperationResult>();

            var ids = result.Details.OfType<BulkOperationResult.PatchDetails>()
                .Select(d => d.Id)
                .ToArray();

            return ids;
        }

        public async Task<string[]> UnArchiveMessages(IEnumerable<string> failedMessageIds, CancellationToken cancellationToken = default)
        {
            Dictionary<string, FailedMessage> failedMessages;

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            session.Advanced.UseOptimisticConcurrency = true;

            var documentIds = failedMessageIds.Select(FailedMessageIdGenerator.MakeDocumentId);

            failedMessages = await session.LoadAsync<FailedMessage>(documentIds, cancellationToken);

            foreach (var failedMessage in failedMessages.Values)
            {
                if (failedMessage.Status == FailedMessageStatus.Archived)
                {
                    failedMessage.Status = FailedMessageStatus.Unresolved;
                    session.Advanced.GetMetadataFor(failedMessage).Remove(Constants.Documents.Metadata.Expires);
                }
            }

            await session.SaveChangesAsync(cancellationToken);

            // Return the unique IDs - the dictionary keys are document ids with a prefix
            return failedMessages.Values.Select(x => x.UniqueMessageId).ToArray();
        }

        public async Task RevertRetry(string messageUniqueId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var failedMessage = await session
                .LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(messageUniqueId), cancellationToken);
            failedMessage?.Status = FailedMessageStatus.Unresolved;

            var failedMessageRetry = await session
                .LoadAsync<FailedMessageRetry>(RetryDocumentDataStore.MakeFailedMessageRetriesDocumentId(messageUniqueId), cancellationToken);
            if (failedMessageRetry != null)
            {
                session.Delete(failedMessageRetry);
            }

            await session.SaveChangesAsync(cancellationToken);
        }

        public async Task RemoveFailedMessageRetry(string uniqueMessageId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            await session.Advanced.RequestExecutor.ExecuteAsync(new DeleteDocumentCommand(RetryDocumentDataStore.MakeFailedMessageRetriesDocumentId(uniqueMessageId), null), session.Advanced.Context, token: cancellationToken);
        }

        public async Task<string[]> GetRetryPendingMessages(DateTime from, DateTime to, string queueAddress, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var query = session
                .Query<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                .Where(o => o.Status == FailedMessageStatus.RetryIssued && o.LastModified >= from.Ticks && o.LastModified <= to.Ticks && o.QueueAddress == queueAddress)
                .OfType<FailedMessageProjection>();

            int index = 0;
            await using var streamResults = await session.Advanced.StreamAsync(query, out var streamQueryStatistics, cancellationToken);
            string[] ids = new string[streamQueryStatistics.TotalResults];
            while (await streamResults.MoveNextAsync())
            {
                ids[index++] = streamResults.Current.Document.UniqueMessageId;
            }
            return ids;
        }

        record struct FailedMessageProjection(string UniqueMessageId);
    }
}
