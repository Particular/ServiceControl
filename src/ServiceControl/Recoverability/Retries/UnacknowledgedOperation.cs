namespace ServiceControl.Recoverability
{
    using System;

    public class UnacknowledgedOperation
    {
        public string RequestId { get; set; }
        public Enum OperationType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime CompletionTime { get; set; }
        public DateTime Last { get; set; }
        public string Originator { get; set; }
        public string Classifier { get; set; }
        public bool Failed { get; set; }
        public int NumberOfMessagesProcessed { get; set; }
    }
}