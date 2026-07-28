namespace ServiceControl.Contracts.MessageRedirects
{
    using System;
    using Infrastructure.DomainEvents;

    public class MessageRedirectCreated : IDomainEvent
    {
        public Guid MessageRedirectId { get; set; }
        public string FromPhysicalAddress { get; set; }
        public string ToPhysicalAddress { get; set; }
    }
}