namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using Infrastructure.DomainEvents;

    class HeartbeatsUpdated : IDomainEvent
    {
        public HeartbeatsUpdated()
        {
#pragma warning disable RS0030 // Do not use banned apis: Runtime events, expiry checks, and monitoring thresholds require current wall-clock time
            RaisedAt = DateTime.UtcNow;
#pragma warning restore RS0030
        }

        public int Active { get; set; }
        public int Failing { get; set; }
        public DateTime RaisedAt { get; set; }
    }
}