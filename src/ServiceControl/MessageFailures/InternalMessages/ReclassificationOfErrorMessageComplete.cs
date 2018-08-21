namespace ServiceControl.MessageFailures.InternalMessages
{
    using Infrastructure.DomainEvents;

    public class ReclassificationOfErrorMessageComplete : IDomainEvent
    {
        public int NumberofMessageReclassified { get; set; }
    }
}