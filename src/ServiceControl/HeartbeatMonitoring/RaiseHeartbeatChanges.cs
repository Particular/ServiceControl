namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using Contracts.HeartbeatMonitoring;
    using NServiceBus;

    public class RaiseHeartbeatChanges :
        IHandleMessages<HeartbeatingEndpointDetected>,
        IHandleMessages<EndpointFailedToHeartbeat>,
        IHandleMessages<EndpointHeartbeatRestored>
    {
        public RaiseHeartbeatChanges(IBus bus)
        {
            this.bus = bus;
        }

        public HeartbeatsComputation HeartbeatsComputation { get; set; }

        public void Handle(EndpointFailedToHeartbeat message)
        {
            PublishUpdate(HeartbeatsComputation.EndpointFailedToHeartbeat());
        }

        public void Handle(EndpointHeartbeatRestored message)
        {
            PublishUpdate(HeartbeatsComputation.EndpointHeartbeatRestored());
        }

        public void Handle(HeartbeatingEndpointDetected message)
        {
            PublishUpdate(HeartbeatsComputation.NewHeartbeatingEndpointDetected());
        }

        void PublishUpdate(HeartbeatsComputation.HeartbeatsStats stats)
        {
            bus.Publish(new HeartbeatsUpdated
            {
                Active = stats.Active,
                Failing = stats.Dead,
                LastUpdatedAt = DateTime.UtcNow
            });
        }

        readonly IBus bus;
    }
}