namespace ServiceControl.Recoverability
{
    using System;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    public class HistoricRetryOperation : IVersionedRow
    {
        public required string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime CompletionTime { get; set; }
        public string? Originator { get; set; }
        public bool Failed { get; set; }
        public int NumberOfMessagesProcessed { get; set; }
        object?[] IVersionedRow.VersionFields =>
            [RequestId, RetryType, StartTime, CompletionTime, Originator, Failed, NumberOfMessagesProcessed];
    }
}