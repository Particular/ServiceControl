namespace ServiceControl.Persistence.EFCore.Infrastructure;

using System;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

static class RetryHistoryQueries
{
    /// <summary>
    /// Every field of every operation in both collections, plus each collection's count
    /// </summary>
    public static QueryStatsInfo ToQueryStatsInfo(this RetryHistory history) =>
        new(DataVersion.OverRows(
                [("historic", history.HistoricOperations.Count), ("unacknowledged", history.UnacknowledgedOperations.Count)],
                Rows(history),
                row => row),
            history.HistoricOperations.Count,
            false);

    static IEnumerable<object[]> Rows(RetryHistory history)
    {
        // The leading marker keeps a historic row from ever digesting the same as an unacknowledged one.
        foreach (var operation in history.HistoricOperations)
        {
            yield return ["historic", operation.RequestId, operation.RetryType, operation.StartTime, operation.CompletionTime,
                operation.Originator, operation.Failed, operation.NumberOfMessagesProcessed];
        }

        // Sorted because these rows are read without an ORDER BY, so the order they arrive in is not a
        // property of the data and must not move the version.
        foreach (var operation in history.UnacknowledgedOperations
            .OrderBy(operation => operation.RequestId, StringComparer.Ordinal)
            .ThenBy(operation => operation.RetryType))
        {
            yield return ["unacknowledged", operation.RequestId, operation.RetryType, operation.StartTime, operation.CompletionTime,
                operation.Last, operation.Originator, operation.Classifier, operation.Failed, operation.NumberOfMessagesProcessed];
        }
    }
}
