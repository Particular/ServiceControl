namespace ServiceControl.Persistence.EFCore.Infrastructure;

using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

static class RetryHistoryQueries
{
    /// <summary>
    /// Every field of every operation in both collections, plus each collection's count.
    /// </summary>
    public static QueryStatsInfo ToQueryStatsInfo(this RetryHistory history) =>
        QueryStatsInfo.Fresh(DataVersion.OverRows(
                [("historic", history.HistoricOperations.Count), ("unacknowledged", history.UnacknowledgedOperations.Count)],
                Rows(history),
                row => row),
            history.HistoricOperations.Count);

    static IEnumerable<object[]> Rows(RetryHistory history)
    {
        // The leading marker keeps a historic row from ever digesting the same as an unacknowledged one.
        foreach (var operation in history.HistoricOperations)
        {
            yield return ["historic", operation.RequestId, operation.RetryType, operation.StartTime, operation.CompletionTime,
                operation.Originator, operation.Failed, operation.NumberOfMessagesProcessed];
        }

        // Rows are named by position, so both collections have to arrive in a deterministic order. Each is
        // ordered by its query in RetryHistoryDataStore, historic by completion time and these by their key.
        foreach (var operation in history.UnacknowledgedOperations)
        {
            yield return ["unacknowledged", operation.RequestId, operation.RetryType, operation.StartTime, operation.CompletionTime,
                operation.Last, operation.Originator, operation.Classifier, operation.Failed, operation.NumberOfMessagesProcessed];
        }
    }
}
