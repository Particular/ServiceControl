namespace ServiceControl.HeartbeatMonitoring
{
    using Contracts.EndpointControl;
    using Contracts.HeartbeatMonitoring;
    using EndpointControl.Contracts;
    using NServiceBus;

    public class RaiseHeartbeatChanges :
        IHandleMessages<HeartbeatingEndpointDetected>,
        IHandleMessages<EndpointFailedToHeartbeat>,
        IHandleMessages<EndpointHeartbeatRestored>,
        IHandleMessages<MonitoringEnabledForEndpoint>,
        IHandleMessages<MonitoringDisabledForEndpoint>,
        IHandleMessages<NewEndpointDetected>
    {
        public IBus Bus { get; set; }
     
        public HeartbeatStatusProvider StatusProvider { get; set; }

        public void Handle(EndpointFailedToHeartbeat message)
        {
            PublishUpdate(StatusProvider.RegisterEndpointThatFailedToHeartbeat(message.Endpoint));
        }

        public void Handle(EndpointHeartbeatRestored message)
        {
            PublishUpdate(StatusProvider.RegisterEndpointWhoseHeartbeatIsRestored(message.Endpoint));
        }

        public void Handle(HeartbeatingEndpointDetected message)
        {
            PublishUpdate(StatusProvider.RegisterHeartbeatingEndpoint(message.Endpoint));
        }

        public void Handle(MonitoringEnabledForEndpoint message)
        {
            PublishUpdate(StatusProvider.EnableMonitoring(message.Endpoint));
        }

        public void Handle(MonitoringDisabledForEndpoint message)
        {

            PublishUpdate(StatusProvider.DisableMonitoring(message.Endpoint));
        }

        public void Handle(NewEndpointDetected message)
        {
            PublishUpdate(StatusProvider.RegisterNewEndpoint(message.Endpoint));
        }

        void PublishUpdate(HeartbeatsStats stats)
        {
            Bus.Publish(new HeartbeatsUpdated
            {
                Active = stats.Active,
                Failing = stats.Dead,
            });
        }



    }
}