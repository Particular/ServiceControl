namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AuditRetentionBuckets;
    using Auditing.MessagesView;
    using Extensions;
    using Indexes;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Session;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.SagaAudit;
    using Transformers;

    class RavenAuditDataStore(IRavenSessionProvider sessionProvider, DatabaseConfiguration databaseConfiguration, AuditRetentionBucketManager auditRetentionBucketManager)
        : IAuditDataStore
    {
        public async Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid input, CancellationToken cancellationToken = default)
        {
            if (databaseConfiguration.EnableAuditRetentionBuckets)
            {
                return await QueryBucketedSagaHistoryById(input, cancellationToken);
            }

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var sagaHistory = await
                session.Query<SagaHistory, SagaDetailsIndex>()
                    .Statistics(out var stats)
                    .SingleOrDefaultAsync(x => x.SagaId == input, token: cancellationToken);

            return sagaHistory == null ? QueryResult<SagaHistory>.Empty() : new QueryResult<SagaHistory>(sagaHistory, new QueryStatsInfo($"{stats.ResultEtag}", stats.TotalResults));
        }

        public async Task<QueryResult<IList<MessagesView>>> GetMessages(bool includeSystemMessages, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange, CancellationToken cancellationToken = default)
        {
            if (databaseConfiguration.EnableAuditRetentionBuckets)
            {
                return await QueryBucketedMessages(query => query
                    .FilterBySentTimeRange(timeSentRange)
                    .IncludeSystemMessagesWhere(includeSystemMessages)
                    .Sort(sortInfo), pagingInfo, sortInfo, cancellationToken);
            }

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var results = await session.Query<MessagesViewIndex.SortAndFilterOptions>(GetIndexName(isFullTextSearchEnabled))
                .Statistics(out var stats)
                .FilterBySentTimeRange(timeSentRange)
                .IncludeSystemMessagesWhere(includeSystemMessages)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .ToMessagesView()
                .ToListAsync(token: cancellationToken);

            return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessages(string searchParam, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange, CancellationToken cancellationToken = default)
        {
            if (databaseConfiguration.EnableAuditRetentionBuckets)
            {
                return await QueryBucketedMessages(query => query
                    .Search(x => x.Query, searchParam)
                    .FilterBySentTimeRange(timeSentRange)
                    .Sort(sortInfo), pagingInfo, sortInfo, cancellationToken);
            }

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var results = await session.Query<MessagesViewIndex.SortAndFilterOptions>(GetIndexName(isFullTextSearchEnabled))
                .Statistics(out var stats)
                .Search(x => x.Query, searchParam)
                .FilterBySentTimeRange(timeSentRange)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .ToMessagesView()
                .ToListAsync(token: cancellationToken);

            return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(string endpoint, string keyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange, CancellationToken cancellationToken = default)
        {
            if (databaseConfiguration.EnableAuditRetentionBuckets)
            {
                return await QueryBucketedMessages(query => query
                    .Search(x => x.Query, keyword)
                    .Where(m => m.ReceivingEndpointName == endpoint)
                    .FilterBySentTimeRange(timeSentRange)
                    .Sort(sortInfo), pagingInfo, sortInfo, cancellationToken);
            }

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var results = await session.Query<MessagesViewIndex.SortAndFilterOptions>(GetIndexName(isFullTextSearchEnabled))
                .Statistics(out var stats)
                .Search(x => x.Query, keyword)
                .Where(m => m.ReceivingEndpointName == endpoint)
                .FilterBySentTimeRange(timeSentRange)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .ToMessagesView()
                .ToListAsync(token: cancellationToken);

            return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(bool includeSystemMessages, string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange, CancellationToken cancellationToken = default)
        {
            if (databaseConfiguration.EnableAuditRetentionBuckets)
            {
                return await QueryBucketedMessages(query => query
                    .IncludeSystemMessagesWhere(includeSystemMessages)
                    .Where(m => m.ReceivingEndpointName == endpointName)
                    .FilterBySentTimeRange(timeSentRange)
                    .Sort(sortInfo), pagingInfo, sortInfo, cancellationToken);
            }

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var results = await session.Query<MessagesViewIndex.SortAndFilterOptions>(GetIndexName(isFullTextSearchEnabled))
                .Statistics(out var stats)
                .IncludeSystemMessagesWhere(includeSystemMessages)
                .Where(m => m.ReceivingEndpointName == endpointName)
                .FilterBySentTimeRange(timeSentRange)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .ToMessagesView()
                .ToListAsync(token: cancellationToken);

            return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken = default)
        {
            if (databaseConfiguration.EnableAuditRetentionBuckets)
            {
                return await QueryBucketedMessages(query => query
                    .Where(m => m.ConversationId == conversationId)
                    .Sort(sortInfo), pagingInfo, sortInfo, cancellationToken);
            }

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var results = await session.Query<MessagesViewIndex.SortAndFilterOptions>(GetIndexName(isFullTextSearchEnabled))
                .Statistics(out var stats)
                .Where(m => m.ConversationId == conversationId)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .ToMessagesView()
                .ToListAsync(token: cancellationToken);

            return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
        }

        public async Task<MessageBodyView> GetMessageBody(string messageId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var result = await session.Advanced.Attachments.GetAsync(messageId, "body", cancellationToken);

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


        public async Task<QueryResult<IList<AuditCount>>> QueryAuditCounts(string endpointName, CancellationToken cancellationToken = default)
        {
            if (databaseConfiguration.EnableAuditRetentionBuckets)
            {
                return await QueryBucketedAuditCounts(endpointName, cancellationToken);
            }

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var indexName = GetIndexName(isFullTextSearchEnabled);

            // Maximum should really be 31 queries if there are 30 days of audit data, but default limit is 30
            session.Advanced.MaxNumberOfRequestsPerSession = 40;

            var results = new List<AuditCount>();

            var oldestMsg = await session.Query<MessagesViewIndex.SortAndFilterOptions>(indexName)
                .Where(m => m.ReceivingEndpointName == endpointName)
                .OrderBy(m => m.ProcessedAt)
                .FirstOrDefaultAsync(token: cancellationToken);

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
                        .ToArrayAsync(token: cancellationToken);

                    if (stats.TotalResults > 0)
                    {
                        results.Add(new AuditCount
                        {
                            UtcDate = date,
                            Count = stats.TotalResults
                        });
                    }
                }
            }

            return new QueryResult<IList<AuditCount>>(results, QueryStatsInfo.Zero);
        }

        async Task<QueryResult<IList<MessagesView>>> QueryBucketedMessages(
            Func<IQueryable<MessagesViewIndex.SortAndFilterOptions>, IQueryable<MessagesViewIndex.SortAndFilterOptions>> query,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            CancellationToken cancellationToken)
        {
            // The retained bucket catalog is snapshotted once per API operation; every batch below
            // queries against this snapshot so a concurrent cleanup cannot change the candidate set
            // mid-operation.
            var buckets = await auditRetentionBucketManager.GetActiveBuckets(cancellationToken);

            // Application-owned index routing: bucket mode never relies on Raven auto-selecting an
            // index alias. The durable bucket catalog entry owns the name of every dedicated static
            // index, GetActiveBuckets returns only the retained (active) catalog entries, and each
            // query below is executed explicitly against that bucket's own index name. Per-bucket
            // results are then merged in memory. The bucket-mode read/paging tests exercise this
            // routing across multiple buckets.

            // Per-bucket queries fan out concurrently in bounded batches of
            // MaxConcurrentBucketQueries, and each concurrent query uses its own session: Raven
            // sessions are not thread-safe and must never be shared across concurrent operations.
            var bucketResults = new List<BucketMessagesResult>(buckets.Count);

            foreach (var batch in buckets.Chunk(MaxConcurrentBucketQueries))
            {
                var tasks = batch.Select(bucket => QueryBucketMessages(bucket, query, pagingInfo, cancellationToken)).ToArray();
                bucketResults.AddRange(await Task.WhenAll(tasks));
            }

            var mergedResults = bucketResults.SelectMany(r => r.Messages).ToList();
            var totalResults = bucketResults.Sum(r => r.TotalResults);
            var etags = bucketResults.Select(r => r.Etag);

            // Each bucket contributes only its first Offset + PageSize candidates (see
            // QueryBucketMessages for why that is sufficient for a globally sorted page), so the
            // merge below is bounded by buckets * (Offset + PageSize), never by the number of
            // matching documents. The same deterministic in-memory ordering as before is applied, so
            // the resulting page is identical to the pre-bounded behavior, and the reported total is
            // the truthful sum of the per-bucket totals.
            var pagedResults = SortInMemory(mergedResults, sortInfo)
                .Skip(pagingInfo.Offset)
                .Take(pagingInfo.PageSize)
                .ToList();

            return new QueryResult<IList<MessagesView>>(pagedResults, new QueryStatsInfo(string.Join(",", etags), totalResults));
        }

        async Task<BucketMessagesResult> QueryBucketMessages(
            AuditRetentionBucket bucket,
            Func<IQueryable<MessagesViewIndex.SortAndFilterOptions>, IQueryable<MessagesViewIndex.SortAndFilterOptions>> query,
            PagingInfo pagingInfo,
            CancellationToken cancellationToken)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            LiftSessionRequestBudget(session);

            var indexName = isFullTextSearchEnabled ? bucket.MessagesViewFullTextIndex : bucket.MessagesViewIndex;

            // The bucket query is composed once with the same filter and sort semantics as the final
            // global result and paged with Skip/Take below.
            var bucketQuery = query(session.Query<MessagesViewIndex.SortAndFilterOptions>(indexName)
                .Statistics(out var stats));

            // Only the first Offset + PageSize candidates per bucket are read instead of the whole
            // matching set. Why is that sufficient for a globally sorted page?
            //
            // The global result is a sorted merge of the per-bucket sorted results: the in-memory
            // OrderBy in SortInMemory is stable and per-bucket results arrive in bucket order, which
            // is exactly a sorted merge under the (sort key, bucket order, in-bucket order) total
            // ordering. In a sorted merge the element at global position k can only originate from
            // position <= k within its own bucket: if it sat at position p > k in that bucket, the
            // p - 1 elements before it there would all precede it in the merge as well, contradicting
            // position k. Every element that can land in global positions
            // Offset + 1 .. Offset + PageSize therefore appears within the first Offset + PageSize
            // candidates of some bucket, and fetching exactly those candidates per bucket, merging,
            // and applying the same stable in-memory ordering reproduces the exact global page. This
            // bounds per-bucket reads by Offset + PageSize index entries no matter how many messages
            // a bucket holds; QueryStatistics.TotalResults is unaffected by Take and still reports
            // the truthful number of matching documents.
            var candidateLimit = (long)pagingInfo.Offset + pagingInfo.PageSize;

            if (candidateLimit == 0)
            {
                // No candidates are needed for this page; run a zero-take query solely to capture the
                // truthful total and etag without materializing any documents.
                _ = await bucketQuery.Take(0).ToArrayAsync(token: cancellationToken);
                return new BucketMessagesResult([], stats.TotalResults, $"{stats.ResultEtag}");
            }

            // RavenDB caps a single query at BucketQueryPageSize results, so the candidates are read
            // in pages of that size, stopping at the candidate limit instead of walking the bucket.
            var messages = new List<MessagesView>();
            var fetched = 0L;
            while (fetched < candidateLimit)
            {
                var take = (int)Math.Min(candidateLimit - fetched, BucketQueryPageSize);
                var page = await bucketQuery
                    .Skip((int)fetched)
                    .Take(take)
                    .ToMessagesView()
                    .ToListAsync(token: cancellationToken);

                if (page.Count == 0)
                {
                    break;
                }

                fetched += page.Count;
                messages.AddRange(page);

                // A bucket holding fewer matches than the candidate limit is exhausted; stop instead
                // of issuing a trailing empty page query.
                if (fetched >= stats.TotalResults)
                {
                    break;
                }
            }

            return new BucketMessagesResult(messages, stats.TotalResults, $"{stats.ResultEtag}");
        }

        async Task<QueryResult<SagaHistory>> QueryBucketedSagaHistoryById(Guid input, CancellationToken cancellationToken)
        {
            var buckets = await auditRetentionBucketManager.GetActiveBuckets(cancellationToken);

            // One request per retained bucket, fanned out concurrently in bounded batches; same
            // application-owned routing and per-session isolation as QueryBucketedMessages.
            var bucketResults = new List<BucketSagaHistoryResult>(buckets.Count);

            foreach (var batch in buckets.Chunk(MaxConcurrentBucketQueries))
            {
                var tasks = batch.Select(bucket => QueryBucketSagaHistory(bucket, input, cancellationToken)).ToArray();
                bucketResults.AddRange(await Task.WhenAll(tasks));
            }

            SagaHistory merged = null;
            var changes = new List<SagaStateChange>();
            var etags = new List<string>();

            foreach (var result in bucketResults)
            {
                if (result.SagaHistory == null)
                {
                    continue;
                }

                merged ??= new SagaHistory
                {
                    Id = result.SagaHistory.Id,
                    SagaId = result.SagaHistory.SagaId,
                    SagaType = result.SagaHistory.SagaType
                };
                changes.AddRange(result.SagaHistory.Changes);
                etags.Add(result.Etag);
            }

            if (merged == null)
            {
                return QueryResult<SagaHistory>.Empty();
            }

            merged.Changes = changes
                .OrderByDescending(x => x.FinishTime)
                .Take(50000)
                .ToList();

            // All per-bucket fragments merge into at most one SagaHistory, so the reported total is
            // 1 when a merged history exists (mirroring legacy mode's SingleOrDefault semantics),
            // never the sum of per-bucket index totals.
            return new QueryResult<SagaHistory>(merged, new QueryStatsInfo(string.Join(",", etags), 1));
        }

        async Task<BucketSagaHistoryResult> QueryBucketSagaHistory(AuditRetentionBucket bucket, Guid input, CancellationToken cancellationToken)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            LiftSessionRequestBudget(session);

            var sagaHistory = await session.Query<SagaHistory>(bucket.SagaDetailsIndex)
                .Statistics(out var stats)
                .SingleOrDefaultAsync(x => x.SagaId == input, token: cancellationToken);

            return new BucketSagaHistoryResult(sagaHistory, $"{stats.ResultEtag}");
        }

        async Task<QueryResult<IList<AuditCount>>> QueryBucketedAuditCounts(string endpointName, CancellationToken cancellationToken)
        {
            var buckets = await auditRetentionBucketManager.GetActiveBuckets(cancellationToken);

            var results = new List<AuditCount>();

            // Oldest message per bucket, fanned out concurrently in bounded batches. Same
            // application-owned routing and per-session isolation as QueryBucketedMessages; no fixed
            // request budget applies.
            DateTime? oldestProcessedAt = null;

            foreach (var batch in buckets.Chunk(MaxConcurrentBucketQueries))
            {
                var tasks = batch.Select(bucket => QueryBucketOldestMessage(bucket, endpointName, cancellationToken)).ToArray();
                var oldestPerBucket = await Task.WhenAll(tasks);

                foreach (var processedAt in oldestPerBucket.Where(r => r.HasValue).Select(r => r.Value))
                {
                    if (oldestProcessedAt == null || processedAt < oldestProcessedAt)
                    {
                        oldestProcessedAt = processedAt;
                    }
                }
            }

            if (oldestProcessedAt != null)
            {
                var endDate = DateTime.UtcNow.Date.AddDays(1);
                var oldestMsgDate = oldestProcessedAt.Value.ToUniversalTime().Date;
                var thirtyDays = endDate.AddDays(-30);

                var startDate = oldestMsgDate > thirtyDays ? oldestMsgDate : thirtyDays;

                for (var date = startDate; date < endDate; date = date.AddDays(1))
                {
                    var nextDate = date.AddDays(1);
                    var count = 0L;

                    // Per-day counts fan out across buckets concurrently in bounded batches.
                    foreach (var batch in buckets.Chunk(MaxConcurrentBucketQueries))
                    {
                        var tasks = batch.Select(bucket => QueryBucketCountForDate(bucket, endpointName, date, nextDate, cancellationToken)).ToArray();
                        count += (await Task.WhenAll(tasks)).Sum();
                    }

                    if (count > 0)
                    {
                        results.Add(new AuditCount
                        {
                            UtcDate = date,
                            Count = count
                        });
                    }
                }
            }

            return new QueryResult<IList<AuditCount>>(results, QueryStatsInfo.Zero);
        }

        async Task<DateTime?> QueryBucketOldestMessage(AuditRetentionBucket bucket, string endpointName, CancellationToken cancellationToken)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            LiftSessionRequestBudget(session);

            var indexName = isFullTextSearchEnabled ? bucket.MessagesViewFullTextIndex : bucket.MessagesViewIndex;

            var oldestMsg = await session.Query<MessagesViewIndex.SortAndFilterOptions>(indexName)
                .Where(m => m.ReceivingEndpointName == endpointName)
                .OrderBy(m => m.ProcessedAt)
                .FirstOrDefaultAsync(token: cancellationToken);

            return oldestMsg?.ProcessedAt;
        }

        async Task<long> QueryBucketCountForDate(AuditRetentionBucket bucket, string endpointName, DateTime date, DateTime nextDate, CancellationToken cancellationToken)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            LiftSessionRequestBudget(session);

            var indexName = isFullTextSearchEnabled ? bucket.MessagesViewFullTextIndex : bucket.MessagesViewIndex;

            _ = await session.Query<MessagesViewIndex.SortAndFilterOptions>(indexName)
                .Statistics(out var stats)
                .Where(m => m.ReceivingEndpointName == endpointName && !m.IsSystemMessage && m.ProcessedAt >= date && m.ProcessedAt < nextDate)
                .Take(0)
                .ToArrayAsync(token: cancellationToken);

            return stats.TotalResults;
        }

        static IEnumerable<MessagesView> SortInMemory(IEnumerable<MessagesView> source, SortInfo sortInfo)
        {
            var ascending = sortInfo.Direction == "asc";

            return sortInfo.Sort switch
            {
                "id" or "message_id" => ascending ? source.OrderBy(m => m.MessageId) : source.OrderByDescending(m => m.MessageId),
                "message_type" => ascending ? source.OrderBy(m => m.MessageType) : source.OrderByDescending(m => m.MessageType),
                "critical_time" => ascending ? source.OrderBy(m => m.CriticalTime) : source.OrderByDescending(m => m.CriticalTime),
                "delivery_time" => ascending ? source.OrderBy(m => m.DeliveryTime) : source.OrderByDescending(m => m.DeliveryTime),
                "processing_time" => ascending ? source.OrderBy(m => m.ProcessingTime) : source.OrderByDescending(m => m.ProcessingTime),
                "processed_at" => ascending ? source.OrderBy(m => m.ProcessedAt) : source.OrderByDescending(m => m.ProcessedAt),
                "status" => ascending ? source.OrderBy(m => m.Status) : source.OrderByDescending(m => m.Status),
                _ => ascending ? source.OrderBy(m => m.TimeSent) : source.OrderByDescending(m => m.TimeSent)
            };
        }

        static string GetIndexName(bool isFullTextSearchEnabled) => isFullTextSearchEnabled ? "MessagesViewIndexWithFullTextSearch" : "MessagesViewIndex";

        // RavenDB caps a single query at this many results. Bucket mode reads per-bucket candidates
        // in pages of this size, stopping after Offset + PageSize candidates per bucket instead of
        // walking the whole matching set.
        const int BucketQueryPageSize = 1024;

        // Bounded fan-out across retained buckets: at most this many per-bucket Raven queries run
        // concurrently per API operation. Each concurrent query uses its own session (Raven sessions
        // are not thread-safe), and batches are drained with Task.WhenAll so a large hourly-bucket
        // catalog cannot flood the server with unbounded parallelism.
        const int MaxConcurrentBucketQueries = 8;

        // The Raven client enforces a per-session request budget (default 30). Bucket mode lifts it
        // entirely: message reads are now bounded to Offset + PageSize candidates per bucket, but
        // saga-history and audit-count reads still issue a request per retained bucket per day (the
        // count path runs up to 31 zero-take queries per bucket), so any fixed ceiling would make
        // those paths fail with "maximum number of requests ... reached" — a correctness bug, not a
        // performance bound. Results must always be complete pages with truthful totals.
        static void LiftSessionRequestBudget(IAsyncDocumentSession session) =>
            session.Advanced.MaxNumberOfRequestsPerSession = int.MaxValue;

        bool isFullTextSearchEnabled = databaseConfiguration.EnableFullTextSearch;

        sealed record BucketMessagesResult(IReadOnlyList<MessagesView> Messages, long TotalResults, string Etag);

        sealed record BucketSagaHistoryResult(SagaHistory SagaHistory, string Etag);
    }
}
