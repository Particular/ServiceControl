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
        new(DataVersion.Compose(
                ("historic", history.HistoricOperations.Count),
                ("historicState", string.Join("|", history.HistoricOperations.Select(operation => FormattableString.Invariant(
                    $"{operation.RequestId}.{operation.RetryType}.{operation.StartTime.Ticks}.{operation.CompletionTime.Ticks}.{operation.Originator}.{operation.Failed}.{operation.NumberOfMessagesProcessed}")))),
                ("unacknowledged", history.UnacknowledgedOperations.Count),
                // Sorted because these rows are read without an ORDER BY, so the order they arrive in is
                // not a property of the data and must not move the version.
                ("unacknowledgedState", string.Join("|", history.UnacknowledgedOperations
                    .OrderBy(operation => operation.RequestId, StringComparer.Ordinal)
                    .ThenBy(operation => operation.RetryType)
                    .Select(operation => FormattableString.Invariant(
                        $"{operation.RequestId}.{operation.RetryType}.{operation.StartTime.Ticks}.{operation.CompletionTime.Ticks}.{operation.Last.Ticks}.{operation.Originator}.{operation.Classifier}.{operation.Failed}.{operation.NumberOfMessagesProcessed}"))))),
            history.HistoricOperations.Count,
            false);
}
