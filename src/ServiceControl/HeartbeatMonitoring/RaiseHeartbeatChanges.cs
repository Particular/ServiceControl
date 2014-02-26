namespace ServiceControl.HeartbeatMonitoring
{
    using Contracts.EndpointControl;
    using Contracts.HeartbeatMonitoring;
    using EndpointControl;
    using NServiceBus;

    public class RaiseHeartbeatChanges :
        IHandleMessages<HeartbeatingEndpointDetected>,
        IHandleMessages<EndpointFailedToHeartbeat>,
        IHandleMessages<EndpointHeartbeatRestored>,
        IHandleMessages<KnownEndpointUpdated>,
        IHandleMessages<NewEndpointDetected>
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

        public void Handle(KnownEndpointUpdated message)
        {
            PublishUpdate(HeartbeatsComputation.Reset());
        }

        public void Handle(NewEndpointDetected message)
        {
            PublishUpdate(HeartbeatsComputation.Reset());
        }

        void PublishUpdate(HeartbeatsComputation.HeartbeatsStats stats)
        {
            bus.Publish(new HeartbeatsUpdated
            {
                Active = stats.Active,
                Failing = stats.Dead,
            });
        }

        readonly IBus bus;
    }
}