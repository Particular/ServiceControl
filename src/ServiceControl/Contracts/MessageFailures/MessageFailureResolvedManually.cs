namespace ServiceControl.Contracts.MessageFailures
{
    using ServiceControl.Infrastructure.DomainEvents;

    public class MessageFailureResolvedManually : IDomainEvent
    {
        public string FailedMessageId { get; set; }
    }
}