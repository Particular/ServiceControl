namespace ServiceControl.Persistence.EFCore.Implementation;

public class RetryStagingStore : IRetryStagingStore
{
    public Task<RetryBatch?> GetStagingBatch() =>
        throw new NotImplementedException();

    public Task<StagingMessage[]> GetMessagesToStage(string batchId) =>
        throw new NotImplementedException();

    public Task MarkBatchAsForwarding(string batchId, string stagingId, IReadOnlyCollection<string> stagedMessageIds) =>
        throw new NotImplementedException();

    public Task DiscardBatch(string batchId) =>
        throw new NotImplementedException();

    public Task<string?> GetForwardingBatchId() =>
        throw new NotImplementedException();

    public Task<RetryBatch?> GetBatch(string batchId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task CompleteForwarding(string batchId) =>
        throw new NotImplementedException();

    public Task RecordStagingFailure(IReadOnlyCollection<string> uniqueMessageIds) =>
        throw new NotImplementedException();

    public Task IncrementStagingAttempts(string uniqueMessageId) =>
        throw new NotImplementedException();

    public Task RemoveFromBatch(string uniqueMessageId) =>
        throw new NotImplementedException();
}
