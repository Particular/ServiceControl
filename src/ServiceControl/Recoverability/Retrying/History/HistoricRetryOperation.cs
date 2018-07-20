namespace ServiceControl.Recoverability
{
    using System;

    public class HistoricRetryOperation
    {
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime CompletionTime { get; set; }
        public string Originator { get; set; }
        public bool Failed { get; set; }
        public int NumberOfMessagesProcessed { get; set; }
    }
}