namespace ServiceControl.CustomChecks
{
    using System;
    using Infrastructure.DomainEvents;

    public class CustomChecksUpdated : IDomainEvent
    {
        public CustomChecksUpdated()
        {
            RaisedAt = DateTime.UtcNow;
        }

        public int Failed { get; set; }
        public DateTime RaisedAt { get; set; }
    }
}