namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using Infrastructure.DomainEvents;

    public class HeartbeatMonitoringEnabled : IDomainEvent
    {
        public Guid EndpointInstanceId { get; set; }
    }
}