namespace ServiceControl.Contracts.MessageRedirects
{
    using System;
    using ServiceControl.Infrastructure.DomainEvents;

    public class MessageRedirectChanged : IDomainEvent
    {
        public Guid MessageRedirectId { get; set; }
        public string FromPhysicalAddress { get; set; }
        public string ToPhysicalAddress { get; set; }
        public string PreviousToPhysicalAddress { get; set; }
    }
}
