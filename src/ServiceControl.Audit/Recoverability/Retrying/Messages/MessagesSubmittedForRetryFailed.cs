namespace ServiceControl.Recoverability
{
    using Infrastructure.DomainEvents;

    public class MessagesSubmittedForRetryFailed : IDomainEvent
    {
        public string FailedMessageId { get; set; }
        public string Destination { get; set; }
        public string Reason { get; set; }
    }
}