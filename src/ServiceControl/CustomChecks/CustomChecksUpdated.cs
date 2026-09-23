namespace ServiceControl.CustomChecks
{
    using System;
    using Infrastructure.DomainEvents;

    public class CustomChecksUpdated : IDomainEvent
    {
        public CustomChecksUpdated()
        {
#pragma warning disable RS0030 // Do not use banned apis: Runtime events, expiry checks, and monitoring thresholds require current wall-clock time
            RaisedAt = DateTime.UtcNow;
#pragma warning restore RS0030
        }

        public int Failed { get; set; }
        public DateTime RaisedAt { get; set; }
    }
}