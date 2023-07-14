namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MessageFailures;
    using ServiceControl.Recoverability;

    public interface IRetryBatchesDataStore
    {
        Task<IRetryBatchesManager> CreateRetryBatchesManager();

        Task RecordFailedStagingAttempt(IReadOnlyCollection<FailedMessage> messages,
            IReadOnlyDictionary<string, FailedMessageRetry> failedMessageRetriesById, Exception e,
            int maxStagingAttempts, string stagingId);

        Task IncrementAttemptCounter(FailedMessageRetry failedMessageRetry);
        Task DeleteFailedMessageRetry(string makeDocumentId);
    }
}