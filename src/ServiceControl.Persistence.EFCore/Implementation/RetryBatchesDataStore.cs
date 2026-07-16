namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.MessageFailures;
using ServiceControl.Recoverability;

public class RetryBatchesDataStore : IRetryBatchesDataStore
{
    public Task<IRetryBatchesManager> CreateRetryBatchesManager() =>
        throw new NotImplementedException();

    public Task RecordFailedStagingAttempt(IReadOnlyCollection<FailedMessage> messages,
        IReadOnlyDictionary<string, FailedMessageRetry> failedMessageRetriesById, Exception e,
        int maxStagingAttempts, string stagingId) =>
        throw new NotImplementedException();

    public Task IncrementAttemptCounter(FailedMessageRetry failedMessageRetry) =>
        throw new NotImplementedException();

    public Task DeleteFailedMessageRetry(string makeDocumentId) =>
        throw new NotImplementedException();
}
