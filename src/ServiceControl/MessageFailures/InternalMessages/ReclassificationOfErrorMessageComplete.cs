namespace ServiceControl.MessageFailures.InternalMessages
{
    using Infrastructure.DomainEvents;

    // TODO: Only used by Raven3.5 storage engine
    public class ReclassificationOfErrorMessageComplete : IDomainEvent
    {
        public int NumberofMessageReclassified { get; set; }
    }
}