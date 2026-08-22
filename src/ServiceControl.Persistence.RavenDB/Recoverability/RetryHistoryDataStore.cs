namespace ServiceControl.Persistence.RavenDB.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Recoverability;

    class RetryHistoryDataStore(IRavenSessionProvider sessionProvider) : IRetryHistoryDataStore
    {
        const string DocumentId = "RetryOperations/History";

        // Before the first operation completes there is no document and so no change vector, but an
        // empty history still has to be cacheable.
        static readonly DataVersion EmptyHistory = DataVersion.FromToken("no-retry-history");

        public async Task<QueryResult<RetryHistory>> GetRetryHistory(CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var retryHistory = await session.LoadAsync<RetryHistory>(DocumentId, cancellationToken);

            // GetChangeVectorFor throws for an entity the session is not tracking, so this relies on the
            // session provider's default. Opening this one with NoTracking would turn the endpoint into a 500.
            var version = retryHistory == null
                ? EmptyHistory
                : DataVersion.FromToken(session.Advanced.GetChangeVectorFor(retryHistory));

            retryHistory ??= new();

            return new QueryResult<RetryHistory>(retryHistory,
                new QueryStatsInfo(version, retryHistory.HistoricOperations.Count));
        }

        public async Task RecordRetryOperationCompleted(string requestId, RetryType retryType, DateTime startTime, DateTime completionTime,
            string originator, string classifier, bool messageFailed, int numberOfMessagesProcessed, DateTime lastProcessed, int retryHistoryDepth,
            CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var retryHistory = await session.LoadAsync<RetryHistory>(DocumentId, cancellationToken) ?? new();

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

            await session.StoreAsync(retryHistory, DocumentId, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> AcknowledgeRetryGroup(string groupId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var retryHistory = await session.LoadAsync<RetryHistory>(DocumentId, cancellationToken);
            if (retryHistory != null)
            {
                if (retryHistory.Acknowledge(groupId, RetryType.FailureGroup))
                {
                    await session.StoreAsync(retryHistory, DocumentId, cancellationToken);
                    await session.SaveChangesAsync(cancellationToken);

                    return true;
                }
            }

            return false;
        }
    }
}