namespace ServiceControl.ExternalIntegrations
{
    using ServiceControl.Contracts;
    using ServiceControl.Contracts.HeartbeatMonitoring;

    public class HeartbeatRestoredPublisher : EventPublisher<EndpointHeartbeatRestored, HeartbeatRestored>
    {
        protected override HeartbeatRestored Convert(EndpointHeartbeatRestored message)
        {
            return new HeartbeatRestored
            {
                RestoredAt = message.RestoredAt,
                Host = message.Endpoint.Host,
                HostId = message.Endpoint.HostId,
                EndpointName = message.Endpoint.Name
            };
        }
    }
}