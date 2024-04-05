﻿namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Auditing.MessagesView;
    using Extensions;
    using Indexes;
    using Monitoring;
    using Raven.Client.Documents;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.Audit.Persistence.Infrastructure;
    using ServiceControl.SagaAudit;
    using Transformers;

    class RavenAuditDataStore(IRavenSessionProvider sessionProvider, DatabaseConfiguration databaseConfiguration)
        : IAuditDataStore
    {
        public async Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid input)
        {
            using var session = sessionProvider.OpenSession();
            var sagaHistory = await
                session.Query<SagaHistory, SagaDetailsIndex>()
                    .Statistics(out var stats)
                    .SingleOrDefaultAsync(x => x.SagaId == input);

            return sagaHistory == null ? QueryResult<SagaHistory>.Empty() : new QueryResult<SagaHistory>(sagaHistory, new QueryStatsInfo($"{stats.ResultEtag}", stats.TotalResults));
        }

        public async Task<QueryResult<IList<MessagesView>>> GetMessages(bool includeSystemMessages, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            using var session = sessionProvider.OpenSession();
            var results = await session.Query<MessagesViewIndex.SortAndFilterOptions>(GetIndexName(isFullTextSearchEnabled))
                .Statistics(out var stats)
                .IncludeSystemMessagesWhere(includeSystemMessages)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .ToMessagesView()
                .ToListAsync();

            return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessages(string searchParam, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            using var session = sessionProvider.OpenSession();
            var results = await session.Query<MessagesViewIndex.SortAndFilterOptions>(GetIndexName(isFullTextSearchEnabled))
                .Statistics(out var stats)
                .Search(x => x.Query, searchParam)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .ToMessagesView()
                .ToListAsync();

            return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(string endpoint, string keyword, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            using var session = sessionProvider.OpenSession();
            var results = await session.Query<MessagesViewIndex.SortAndFilterOptions>(GetIndexName(isFullTextSearchEnabled))
                .Statistics(out var stats)
                .Search(x => x.Query, keyword)
                .Where(m => m.ReceivingEndpointName == endpoint)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .ToMessagesView()
                .ToListAsync();

            return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(bool includeSystemMessages, string endpointName, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            using var session = sessionProvider.OpenSession();
            var results = await session.Query<MessagesViewIndex.SortAndFilterOptions>(GetIndexName(isFullTextSearchEnabled))
                .Statistics(out var stats)
                .IncludeSystemMessagesWhere(includeSystemMessages)
                .Where(m => m.ReceivingEndpointName == endpointName)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .ToMessagesView()
                .ToListAsync();

            return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            using var session = sessionProvider.OpenSession();
            var results = await session.Query<MessagesViewIndex.SortAndFilterOptions>(GetIndexName(isFullTextSearchEnabled))
                .Statistics(out var stats)
                .Where(m => m.ConversationId == conversationId)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .ToMessagesView()
                .ToListAsync();

            return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
        }

        public async Task<MessageBodyView> GetMessageBody(string messageId)
        {
            using var session = sessionProvider.OpenSession();
            var result = await session.Advanced.Attachments.GetAsync(messageId, "body");

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

        public async Task<QueryResult<IList<KnownEndpointsView>>> QueryKnownEndpoints()
        {
            using var session = sessionProvider.OpenSession();
            var endpoints = await session.Advanced.LoadStartingWithAsync<KnownEndpoint>(KnownEndpoint.CollectionName, pageSize: 1024);

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

        public async Task<QueryResult<IList<AuditCount>>> QueryAuditCounts(string endpointName)
        {
            var indexName = GetIndexName(isFullTextSearchEnabled);

            using var session = sessionProvider.OpenSession();
            // Maximum should really be 31 queries if there are 30 days of audit data, but default limit is 30
            session.Advanced.MaxNumberOfRequestsPerSession = 40;

            var results = new List<AuditCount>();

            var oldestMsg = await session.Query<MessagesViewIndex.SortAndFilterOptions>(indexName)
                .Where(m => m.ReceivingEndpointName == endpointName)
                .OrderBy(m => m.ProcessedAt)
                .FirstOrDefaultAsync();

            if (oldestMsg != null)
            {
                var endDate = DateTime.UtcNow.Date.AddDays(1);
                var oldestMsgDate = oldestMsg.ProcessedAt.ToUniversalTime().Date;
                var thirtyDays = endDate.AddDays(-30);

                var startDate = oldestMsgDate > thirtyDays ? oldestMsgDate : thirtyDays;

                for (var date = startDate; date < endDate; date = date.AddDays(1))
                {
                    var nextDate = date.AddDays(1);

                    _ = await session.Query<MessagesViewIndex.SortAndFilterOptions>(indexName)
                        .Statistics(out var stats)
                        .Where(m => m.ReceivingEndpointName == endpointName && !m.IsSystemMessage && m.ProcessedAt >= date && m.ProcessedAt < nextDate)
                        .Take(0)
                        .ToArrayAsync();

                    if (stats.TotalResults > 0)
                    {
                        results.Add(new AuditCount { UtcDate = date, Count = stats.TotalResults });
                    }
                }
            }

            return new QueryResult<IList<AuditCount>>(results, QueryStatsInfo.Zero);
        }

        static string GetIndexName(bool isFullTextSearchEnabled) => isFullTextSearchEnabled ? "MessagesViewIndexWithFullTextSearch" : "MessagesViewIndex";

        bool isFullTextSearchEnabled = databaseConfiguration.EnableFullTextSearch;
    }
}