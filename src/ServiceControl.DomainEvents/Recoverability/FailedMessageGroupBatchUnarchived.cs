namespace ServiceControl.Recoverability
{
    using Infrastructure.DomainEvents;

    public class FailedMessageGroupBatchUnarchived : IDomainEvent
    {
        public string[] FailedMessagesIds { get; set; }
    }
}