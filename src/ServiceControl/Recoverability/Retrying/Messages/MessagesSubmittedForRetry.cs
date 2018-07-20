namespace ServiceControl.Recoverability
{
    using Infrastructure.DomainEvents;

    public class MessagesSubmittedForRetry : IDomainEvent
    {
        public string Context { get; set; }
        public string[] FailedMessageIds { get; set; }
        public int NumberOfFailedMessages { get; set; }
    }
}