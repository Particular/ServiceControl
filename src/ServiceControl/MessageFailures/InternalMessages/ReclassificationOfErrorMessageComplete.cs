namespace ServiceControl.MessageFailures.InternalMessages
{
    using ServiceControl.Infrastructure.DomainEvents;

    public class ReclassificationOfErrorMessageComplete : IDomainEvent
    {
        public int NumberofMessageReclassified { get; set; }
    }
}