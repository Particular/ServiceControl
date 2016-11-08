namespace ServiceControl.EndpointControl.Handlers
{
    using Infrastructure;
    using InternalMessages;
    using NServiceBus;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.Contracts.HeartbeatMonitoring;

    class DetectEndpointsFromHeartbeats : 
        IHandleMessages<HeartbeatingEndpointDetected>,
        IHandleMessages<EndpointHeartbeatRestored>
    {
        public IBus Bus { get; set; }

        public KnownEndpointsCache KnownEndpointsCache { get; set; }

        public void Handle(HeartbeatingEndpointDetected message)
        {
            var id = DeterministicGuid.MakeId(message.Endpoint.Name, message.Endpoint.HostId.ToString());

            if (KnownEndpointsCache.TryAdd(id))
            {
                Bus.SendLocal(new RegisterEndpoint
                {
                    EndpointInstanceId = id,
                    Endpoint = message.Endpoint,
                    DetectedAt = message.DetectedAt
                });
            }

            Bus.SendLocal(new EnableEndpointMonitoring
            {
                EndpointId = id
            });
        }

        public void Handle(EndpointHeartbeatRestored message)
        {
            var id = DeterministicGuid.MakeId(message.Endpoint.Name, message.Endpoint.HostId.ToString());
            Bus.SendLocal(new EnableEndpointMonitoring
            {
                EndpointId = id
            });
        }
    }
}