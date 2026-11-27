namespace ServiceControl.Persistence.EFCore.DataMigration;

using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Recoverability;

/// <summary>
/// Turns a retry operation from the source into the row the target stores.
/// Times are stored as UTC, and a time with no kind is taken as UTC already.
/// </summary>
static class RetryOperationRowMapper
{
    public static HistoricRetryOperationEntity Map(HistoricRetryOperation operation) => new()
    {
        RequestId = operation.RequestId,
        RetryType = operation.RetryType,
        StartTime = operation.StartTime,
        CompletionTime = operation.CompletionTime,
        Originator = operation.Originator,
        Failed = operation.Failed,
        NumberOfMessagesProcessed = operation.NumberOfMessagesProcessed
    };

    public static UnacknowledgedRetryOperationEntity Map(UnacknowledgedRetryOperation operation) => new()
    {
        RequestId = operation.RequestId,
        RetryType = operation.RetryType,
        StartTime = operation.StartTime,
        CompletionTime = operation.CompletionTime,
        Last = operation.Last,
        Originator = operation.Originator,
        Classifier = operation.Classifier,
        Failed = operation.Failed,
        NumberOfMessagesProcessed = operation.NumberOfMessagesProcessed
    };
}
