namespace ServiceControl.Recoverability
{
    using System;
    using Infrastructure.DomainEvents;
    using Infrastructure.SignalR;

    public class RetryOperationCompleted : IDomainEvent, IUserInterfaceEvent
    {
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public string Originator { get; set; }
        public string Classifier { get; set; }
        public bool Failed { get; set; }
        public RetryProgress Progress { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime CompletionTime { get; set; }
        public int NumberOfMessagesProcessed { get; set; }
        public DateTime Last { get; set; }
    }
}