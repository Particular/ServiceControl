namespace ServiceControl.Contracts.MessageRedirects
{
    using System;
    using Infrastructure.DomainEvents;
    using Infrastructure.SignalR;

    public class MessageRedirectCreated : IDomainEvent, IUserInterfaceEvent
    {
        public Guid MessageRedirectId { get; set; }
        public string FromPhysicalAddress { get; set; }
        public string ToPhysicalAddress { get; set; }
    }
}