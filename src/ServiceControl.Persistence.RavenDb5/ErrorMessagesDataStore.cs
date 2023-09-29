namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Editing;
    using NServiceBus.Logging;
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
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Recoverability;

    class ErrorMessagesDataStore : IErrorMessageDataStore
    {
        readonly IDocumentStore documentStore;
        readonly TimeSpan eventsRetentionPeriod;

        public ErrorMessagesDataStore(IDocumentStore documentStore, RavenDBPersisterSettings settings)
        {
            this.documentStore = documentStore;
            eventsRetentionPeriod = settings.EventsRetentionPeriod;
        }

        public async Task<QueryResult<IList<MessagesView>>> GetAllMessages(
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            bool includeSystemMessages
            )
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var query = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .IncludeSystemMessagesWhere(includeSystemMessages)
                    .Statistics(out var stats)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .ProjectInto<FailedMessage>()
                    .TransformToMessageView();

                var results = await query.ToListAsync();

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
                var query = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .IncludeSystemMessagesWhere(includeSystemMessages)
                    .Where(m => m.ReceivingEndpointName == endpointName)
                    .Statistics(out var stats)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .ProjectInto<FailedMessage>()
                    .TransformToMessageView();

                var results = await query.ToListAsync();


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
                var query = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Search(x => x.Query, searchKeyword)
                    .Where(m => m.ReceivingEndpointName == endpointName)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .ProjectInto<FailedMessage>()
                    .TransformToMessageView();

                var results = await query.ToListAsync();

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
                var query = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Where(m => m.ConversationId == conversationId)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .ProjectInto<FailedMessage>()
                    .TransformToMessageView();

                var results = await query.ToListAsync();

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
                var query = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Search(x => x.Query, searchTerms)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .ProjectInto<FailedMessage>()
                    .TransformToMessageView();

                var results = await query.ToListAsync();

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        // TODO: There seem to be several copies of this operation in here
        public async Task<FailedMessage> FailedMessageFetch(string failedMessageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                return await session.LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(failedMessageId));
            }
        }

        public async Task FailedMessageMarkAsArchived(string failedMessageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var failedMessage = await session.LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(failedMessageId));

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
                var docIds = ids.Select(g => FailedMessageIdGenerator.MakeDocumentId(g.ToString()));
                var results = await session.LoadAsync<FailedMessage>(docIds);
                return results.Values.Where(x => x != null).ToArray();
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
                    .GetQueryResultAsync();

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
        }

        public async Task<IDictionary<string, object>> ErrorsSummary()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
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
        }

        public async Task<FailedMessage> ErrorBy(Guid failedMessageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var message = await session.LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(failedMessageId.ToString()));
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
                var message = await session.LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(failedMessageId.ToString()));
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
                    .GetQueryResultAsync();

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
            var documentId = FailedMessageIdGenerator.MakeDocumentId(failedMessageId);

            using (var session = documentStore.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var failedMessage = await session.LoadAsync<FailedMessage>(documentId);

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

                await using (var ie = await session.Advanced.StreamAsync(query))
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
            // TODO: Make sure this new implementation actually works, not going to delete the old implementation (commented below) until then
            var patch = new PatchByQueryOperation(new IndexQuery
            {
                Query = $@"from index '{new FailedMessageViewIndex().IndexName} as msg
                           where msg.LastModified >= args.From and msg.LastModified <= args.To
                           where msg.Status == args.Archived
                           update
                           {{
                                msg.Status = args.Unresolved
                           }}",
                QueryParameters =
                {
                    { "From", from },
                    { "To", to },
                    { "Unresolved", (int)FailedMessageStatus.Unresolved },
                    { "Archived", (int)FailedMessageStatus.Archived },
                }
            }, new QueryOperationOptions
            {
                AllowStale = true,
                RetrieveDetails = true
            });

            var operation = await documentStore.Operations.SendAsync(patch);

            var result = await operation.WaitForCompletionAsync<BulkOperationResult>();

            var ids = result.Details.OfType<BulkOperationResult.PatchDetails>()
                .Select(d => d.Id)
                .ToArray();

            // TODO: Are we *really* returning an array AND the length of the same array?
            return (ids, ids.Length);

            //            var options = new BulkOperationOptions
            //            {
            //                AllowStale = true
            //            };

            //            var result = await documentStore.AsyncDatabaseCommands.UpdateByIndexAsync(
            //                new FailedMessageViewIndex().IndexName,
            //                new IndexQuery
            //                {
            //                    Query = string.Format(CultureInfo.InvariantCulture, "LastModified:[{0} TO {1}] AND Status:{2}", from.Ticks, to.Ticks, (int)FailedMessageStatus.Archived),
            //                    Cutoff = cutOff
            //                }, new ScriptedPatchRequest
            //                {
            //                    Script = @"
            //if(this.Status === archivedStatus) {
            //    this.Status = unresolvedStatus;
            //}
            //",
            //                    Values =
            //                    {
            //                        {"archivedStatus", (int)FailedMessageStatus.Archived},
            //                        {"unresolvedStatus", (int)FailedMessageStatus.Unresolved}
            //                    }
            //                }, options);

            //            var patchedDocumentIds = (await result.WaitForCompletionAsync())
            //                .JsonDeserialization<DocumentPatchResult[]>();

            //            return (
            //                patchedDocumentIds.Select(x => FailedMessageIdGenerator.GetMessageIdFromDocumentId(x.Document)).ToArray(),
            //                patchedDocumentIds.Length
            //                );
        }

        public async Task<(string[] ids, int count)> UnArchiveMessages(IEnumerable<string> failedMessageIds)
        {
            Dictionary<string, FailedMessage> failedMessages;

            using (var session = documentStore.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var documentIds = failedMessageIds.Select(FailedMessageIdGenerator.MakeDocumentId);

                failedMessages = await session.LoadAsync<FailedMessage>(documentIds);

                foreach (var failedMessage in failedMessages.Values)
                {
                    if (failedMessage.Status == FailedMessageStatus.Archived)
                    {
                        failedMessage.Status = FailedMessageStatus.Unresolved;
                    }
                }

                await session.SaveChangesAsync();
            }

            return (
                failedMessages.Values.Select(x => x.UniqueMessageId).ToArray(), // TODO: (ramon) I don't think we can use Keys here as UniqueMessageId is something different than failedMessageId right?
                failedMessages.Count
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

        public async Task RemoveFailedMessageRetryDocument(string uniqueMessageId)
        {
            using var session = documentStore.OpenAsyncSession();
            await session.Advanced.RequestExecutor.ExecuteAsync(new DeleteDocumentCommand(FailedMessageRetry.MakeDocumentId(uniqueMessageId), null), session.Advanced.Context);
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
                .WhereBetween(options => options.LastModified, from.Ticks, to.Ticks)
                .AndAlso()
                    .WhereEquals(o => o.QueueAddress, queueAddress)
                    .SelectFields<FailedMessage>()
                    .ToQueryable()
                    .TransformToFailedMessageView();

                await using (var ie = await session.Advanced.StreamAsync(query))
                {
                    while (await ie.MoveNextAsync())
                    {
                        ids.Add(ie.Current.Document.Id);
                    }
                }
            }

            return ids.ToArray();
        }

        public Task<byte[]> FetchFromFailedMessage(string uniqueMessageId)
        {
            throw new NotSupportedException("Body not stored embedded");
        }

        public async Task StoreEventLogItem(EventLogItem logItem)
        {
            var expiration = DateTime.UtcNow + eventsRetentionPeriod;

            using (var session = documentStore.OpenAsyncSession())
            {
                await session.StoreAsync(logItem);

                session.Advanced.GetMetadataFor(logItem)[Constants.Documents.Metadata.Expires] = expiration;

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
