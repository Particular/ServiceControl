namespace ServiceControl.Recoverability
{
    using Infrastructure.DomainEvents;

    public class MessageFailedInStaging : IDomainEvent
    {
        public string UniqueMessageId { get; set; }
    }
}