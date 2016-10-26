namespace ServiceControl.ExternalIntegrations
{
    using ServiceControl.Contracts;
    using ServiceControl.Contracts.HeartbeatMonitoring;

    public class HeartbeatStoppedPublisher : EventPublisher<EndpointFailedToHeartbeat, HeartbeatStopped>
    {
        protected override HeartbeatStopped Convert(EndpointFailedToHeartbeat message)
        {
            return new HeartbeatStopped
            {
                DetectedAt = message.DetectedAt,
                LastReceivedAt = message.LastReceivedAt,
                Host = message.Endpoint.Host,
                HostId = message.Endpoint.HostId,
                EndpointName = message.Endpoint.Name
            };
        }
    }
}