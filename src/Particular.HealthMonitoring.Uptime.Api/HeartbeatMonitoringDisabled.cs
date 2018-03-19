namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using ServiceControl.Infrastructure.DomainEvents;

    public class HeartbeatMonitoringDisabled : IDomainEvent
    {
        public Guid EndpointInstanceId { get; set; }
    }
}