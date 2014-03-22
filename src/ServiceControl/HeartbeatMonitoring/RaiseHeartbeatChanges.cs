namespace ServiceControl.HeartbeatMonitoring
{
    using Contracts.EndpointControl;
    using Contracts.HeartbeatMonitoring;
    using NServiceBus;

    public class RaiseHeartbeatChanges :
        IHandleMessages<HeartbeatStatusChanged>,
        IHandleMessages<NewEndpointDetected>
    {
        public IBus Bus { get; set; }

        public HeartbeatStatusProvider StatusProvider { get; set; }

        public void Handle(HeartbeatStatusChanged message)
        {
            PublishUpdate(StatusProvider.GetHeartbeatsStats());
        }

        public void Handle(NewEndpointDetected message)
        {
            //this call is non intuitive, we just call it since endpoints without the heartbeat plugin installed should count as "failing"
            // we need to revisit the requirements for this
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