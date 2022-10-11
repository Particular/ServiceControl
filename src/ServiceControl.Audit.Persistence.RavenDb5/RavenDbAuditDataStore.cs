﻿namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Auditing.MessagesView;
    using ServiceControl.SagaAudit;
    using Monitoring;
    using Extensions;
    using Indexes;
    using Transformers;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.Audit.Persistence.Infrastructure;

    class RavenDbAuditDataStore : IAuditDataStore
    {
        public RavenDbAuditDataStore(IRavenDbSessionProvider sessionProvider, DatabaseConfiguration databaseConfiguration)
        {
            this.sessionProvider = sessionProvider;
            isFullTextSearchEnabled = databaseConfiguration.EnableFullTextSearch;
        }

        public async Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid input)
        {
            using (var session = sessionProvider.OpenSession())
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

                return new QueryResult<SagaHistory>(sagaHistory, new QueryStatsInfo($"{stats.ResultEtag}", stats.TotalResults));
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> GetMessages(bool includeSystemMessages, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            using (var session = sessionProvider.OpenSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions>(GetIndexName(isFullTextSearchEnabled))
                    .Statistics(out var stats)
                    .IncludeSystemMessagesWhere(includeSystemMessages)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .ToMessagesView()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessages(string searchParam, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            using (var session = sessionProvider.OpenSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions>(GetIndexName(isFullTextSearchEnabled))
                    .Statistics(out var stats)
                    .Search(x => x.Query, searchParam)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .ToMessagesView()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(string endpoint, string keyword, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            using (var session = sessionProvider.OpenSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions>(GetIndexName(isFullTextSearchEnabled))
                    .Statistics(out var stats)
                    .Search(x => x.Query, keyword)
                    .Where(m => m.ReceivingEndpointName == endpoint)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .ToMessagesView()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(bool includeSystemMessages, string endpointName, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            using (var session = sessionProvider.OpenSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions>(GetIndexName(isFullTextSearchEnabled))
                    .Statistics(out var stats)
                    .IncludeSystemMessagesWhere(includeSystemMessages)
                    .Where(m => m.ReceivingEndpointName == endpointName)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .ToMessagesView()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            using (var session = sessionProvider.OpenSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions>(GetIndexName(isFullTextSearchEnabled))
                    .Statistics(out var stats)
                    .Where(m => m.ConversationId == conversationId)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .ToMessagesView()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<MessageBodyView> GetMessageBody(string messageId)
        {
            using (var session = sessionProvider.OpenSession())
            {
                var result = await session.Advanced.Attachments.GetAsync(messageId, "body").ConfigureAwait(false);

                if (result == null)
                {
                    return MessageBodyView.NoContent();
                }

                return MessageBodyView.FromStream(
                    result.Stream,
                    result.Details.ContentType,
                    (int)result.Details.Size,
                    result.Details.ChangeVector
                );
            }
        }

        public async Task<QueryResult<IList<KnownEndpointsView>>> QueryKnownEndpoints()
        {
            using (var session = sessionProvider.OpenSession())
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

        string GetIndexName(bool isFullTextSearchEnabled)
        {
            return isFullTextSearchEnabled ? "MessagesViewIndexWithFullTextSearch" : "MessagesViewIndex";
        }

        bool isFullTextSearchEnabled;

        readonly IRavenDbSessionProvider sessionProvider;
    }
}