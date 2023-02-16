namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Auditing.MessagesView;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.Audit.Persistence.Infrastructure;
    using ServiceControl.Audit.Persistence.Monitoring;
    using ServiceControl.Audit.Persistence.RavenDb.Extensions;
    using ServiceControl.Audit.Persistence.RavenDb.Indexes;
    using ServiceControl.Audit.Persistence.RavenDb.Transformers;
    using ServiceControl.SagaAudit;

    class RavenDbAuditDataStore : IAuditDataStore
    {
        public RavenDbAuditDataStore(IDocumentStore store)
        {
            documentStore = store;
        }

        public async Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid input)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var sagaHistory = await
                    session.Query<SagaHistory, SagaDetailsIndex>()
                        .Statistics(out var stats)
                        .SingleOrDefaultAsync(x => x.SagaId == input)
                        .ConfigureAwait(false);

                if (sagaHistory == null)
                {
                    return QueryResult<SagaHistory>.Empty();
                }

                return new QueryResult<SagaHistory>(sagaHistory, new QueryStatsInfo(stats.IndexEtag, stats.TotalResults));
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> GetMessages(bool includeSystemMessages, PagingInfo pagingInfo, SortInfo sortInfo)
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

        public async Task<QueryResult<IList<MessagesView>>> QueryMessages(string searchParam, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Search(x => x.Query, searchParam)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(string endpoint, string keyword, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Search(x => x.Query, keyword)
                    .Where(m => m.ReceivingEndpointName == endpoint)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(bool includeSystemMessages, string endpointName, PagingInfo pagingInfo, SortInfo sortInfo)
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

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo)
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

        public async Task<MessageBodyView> GetMessageBody(string messageId)
        {
            var fromIndex = await GetMessageBodyFromIndex(messageId).ConfigureAwait(false);

            if (!fromIndex.Found)
            {
                var fromAttachments = await GetMessageBodyFromAttachments(messageId).ConfigureAwait(false);
                if (fromAttachments.Found)
                {
                    return fromAttachments;
                }
            }

            return fromIndex;
        }

        async Task<MessageBodyView> GetMessageBodyFromIndex(string messageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var message = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .TransformWith<MessagesBodyTransformer, MessagesBodyTransformer.Result>()
                    .FirstOrDefaultAsync(f => f.MessageId == messageId)
                    .ConfigureAwait(false);

                if (message == null)
                {
                    return MessageBodyView.NotFound();
                }

                if (message.BodyNotStored && message.Body == null)
                {
                    return MessageBodyView.NoContent();
                }

                if (message.Body == null)
                {
                    return MessageBodyView.NotFound();
                }

                return MessageBodyView.FromString(message.Body, message.ContentType, message.BodySize, stats.IndexEtag);
            }
        }

        async Task<MessageBodyView> GetMessageBodyFromAttachments(string messageId)
        {
            //We want to continue using attachments for now
#pragma warning disable 618
            var attachment = await documentStore.AsyncDatabaseCommands.GetAttachmentAsync($"messagebodies/{messageId}").ConfigureAwait(false);
#pragma warning restore 618

            if (attachment == null)
            {
                return MessageBodyView.NoContent();
            }

            return MessageBodyView.FromStream(
                attachment.Data(),
                attachment.Metadata["ContentType"].Value<string>(),
                attachment.Metadata["ContentLength"].Value<int>(),
                attachment.Etag
            );
        }

        public async Task<QueryResult<IList<KnownEndpointsView>>> QueryKnownEndpoints()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var endpoints = await session.Advanced.LoadStartingWithAsync<KnownEndpoint>(KnownEndpoint.CollectionName, pageSize: 1024)
                    .ConfigureAwait(false);

                var knownEndpoints = endpoints
                    .Select(x => new KnownEndpointsView
                    {
                        Id = DeterministicGuid.MakeId(x.Name, x.HostId.ToString()),
                        EndpointDetails = new EndpointDetails
                        {
                            Host = x.Host,
                            HostId = x.HostId,
                            Name = x.Name
                        },
                        HostDisplayName = x.Host
                    })
                    .ToList();

                return new QueryResult<IList<KnownEndpointsView>>(knownEndpoints, new QueryStatsInfo(string.Empty, knownEndpoints.Count));
            }
        }

        public async Task<QueryResult<IList<AuditCount>>> QueryAuditCounts(string endpointName)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                // Maximum should really be 31 queries if there are 30 days of audit data, but default limit is 30
                session.Advanced.MaxNumberOfRequestsPerSession = 40;

                var results = new List<AuditCount>();

                var oldestMsg = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Where(m => m.ReceivingEndpointName == endpointName)
                    .OrderBy(m => m.ProcessedAt)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                if (oldestMsg != null)
                {
                    var endDate = DateTime.UtcNow.Date.AddDays(1);
                    var oldestMsgDate = oldestMsg.ProcessedAt.ToUniversalTime().Date;
                    var thirtyDays = endDate.AddDays(-30);

                    var startDate = oldestMsgDate > thirtyDays ? oldestMsgDate : thirtyDays;

                    for (var date = startDate; date < endDate; date = date.AddDays(1))
                    {
                        var nextDate = date.AddDays(1);

                        _ = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                            .Statistics(out var stats)
                            .Where(m => m.ReceivingEndpointName == endpointName && !m.IsSystemMessage && m.ProcessedAt >= date && m.ProcessedAt < nextDate)
                            .Take(0)
                            .ToListAsync()
                            .ConfigureAwait(false);

                        if (stats.TotalResults > 0)
                        {
                            results.Add(new AuditCount { UtcDate = date, Count = stats.TotalResults });
                        }
                    }
                }

                return new QueryResult<IList<AuditCount>>(results, QueryStatsInfo.Zero);
            }
        }

        public Task Setup() => Task.CompletedTask;

        IDocumentStore documentStore;
    }
}