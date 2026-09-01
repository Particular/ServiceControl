namespace ServiceControl.Recoverability
{
    using System;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    public class UnacknowledgedRetryOperation : IVersionedRow
    {
        public required string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime CompletionTime { get; set; }
        public DateTime Last { get; set; }
        public string? Originator { get; set; }
        public string? Classifier { get; set; }
        public bool Failed { get; set; }
        public int NumberOfMessagesProcessed { get; set; }
        object?[] IVersionedRow.GetVersionFields() =>
            [RequestId, RetryType, StartTime, CompletionTime, Last, Originator, Classifier, Failed, NumberOfMessagesProcessed];
    }
}