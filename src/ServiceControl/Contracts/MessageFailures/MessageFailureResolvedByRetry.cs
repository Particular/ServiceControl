namespace ServiceControl.Contracts.MessageFailures
{
    using Infrastructure.DomainEvents;

    public partial class MessageFailureResolvedByRetry : IDomainEvent
    {
        public string FailedMessageId { get; set; }
        public string[] AlternativeFailedMessageIds { get; set; }
    }
}