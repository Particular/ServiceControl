namespace ServiceControl.CustomChecks
{
    using System;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Infrastructure.SignalR;

    internal class CustomChecksUpdated : IDomainEvent, IUserInterfaceEvent
    {
        public CustomChecksUpdated()
        {
            RaisedAt = DateTime.UtcNow;
        }

        public int Failed { get; set; }
        public DateTime RaisedAt { get; set; }
    }
}