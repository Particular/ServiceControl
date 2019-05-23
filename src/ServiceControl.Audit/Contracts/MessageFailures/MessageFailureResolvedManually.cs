namespace ServiceControl.Contracts.MessageFailures
{
    using Infrastructure.DomainEvents;

    public class MessageFailureResolvedManually : IDomainEvent
    {
        public string FailedMessageId { get; set; }
    }
}