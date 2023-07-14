namespace ServiceControl.MessageFailures.InternalMessages
{
    using System;
    using Infrastructure.DomainEvents;

    [Obsolete("Only used by legacy RavenDB35 storage engine")] // TODO: how to deal with this domain event
    public class ReclassificationOfErrorMessageComplete : IDomainEvent
    {
        public int NumberofMessageReclassified { get; set; }
    }
}