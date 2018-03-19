namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Infrastructure.SignalR;

    class HeartbeatsUpdated : IDomainEvent, IUserInterfaceEvent
    {
        public HeartbeatsUpdated()
        {
            RaisedAt = DateTime.UtcNow;
        }

        public int Active { get; set; }
        public int Failing { get; set; }
        public DateTime RaisedAt { get; set; }
    }
}