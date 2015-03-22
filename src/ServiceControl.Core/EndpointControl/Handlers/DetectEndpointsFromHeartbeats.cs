namespace ServiceControl.EndpointControl.Handlers
{
    using Infrastructure;
    using InternalMessages;
    using NServiceBus;
    using ServiceControl.Contracts.HeartbeatMonitoring;

    class DetectEndpointsFromHeartbeats : IHandleMessages<HeartbeatingEndpointDetected>
    {
        public IBus Bus { get; set; }

        public KnownEndpointsCache KnownEndpointsCache { get; set; }

        public void Handle(HeartbeatingEndpointDetected message)
        {
            var id = DeterministicGuid.MakeId(message.Endpoint.Name, message.Endpoint.HostId);

            if (KnownEndpointsCache.TryAdd(id))
            {
                Bus.SendLocal(new RegisterEndpoint
                {
                    EndpointInstanceId = id,
                    Endpoint = message.Endpoint,
                    DetectedAt = message.DetectedAt
                });
            }
        }
    }
}