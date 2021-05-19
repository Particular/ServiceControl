namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using Infrastructure.DomainEvents;

    public class HeartbeatMonitoringDisabled : IDomainEvent
    {
        public Guid EndpointInstanceId { get; set; }
    }
}