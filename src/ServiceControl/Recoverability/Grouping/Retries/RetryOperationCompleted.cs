namespace ServiceControl.Recoverability
{
    using System;
    using NServiceBus;

    public class RetryOperationCompleted : IEvent
    {
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public string Originator { get; set; }
        public bool Failed { get; set; }
        public double Progression { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime CompletionTime { get; set; }
    }
}