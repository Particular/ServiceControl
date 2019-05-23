namespace ServiceControl.CustomChecks
{
    using System;
    using Infrastructure.DomainEvents;
    using Infrastructure.SignalR;

    public class CustomChecksUpdated : IDomainEvent, IUserInterfaceEvent
    {
        public CustomChecksUpdated()
        {
            RaisedAt = DateTime.UtcNow;
        }

        public int Failed { get; set; }
        public DateTime RaisedAt { get; set; }
    }
}