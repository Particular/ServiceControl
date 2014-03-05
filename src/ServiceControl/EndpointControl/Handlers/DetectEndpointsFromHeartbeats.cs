namespace ServiceControl.EndpointControl.Handlers
{
    using Contracts.HeartbeatMonitoring;
    using InternalMessages;
    using NServiceBus;

    class DetectEndpointsFromHeartbeats : IHandleMessages<HeartbeatingEndpointDetected>
    {
        public IBus Bus { get; set; }

        public KnownEndpointsCache KnownEndpointsCache { get; set; }

        public void Handle(HeartbeatingEndpointDetected message)
        {
            var id = message.EndpointDetails.Name + message.EndpointDetails.Host;

            if (KnownEndpointsCache.TryAdd(id))
            {
                Bus.SendLocal(new RegisterEndpoint
                {
                    Endpoint = message.EndpointDetails,
                    DetectedAt = message.DetectedAt
                });
            }
        }
    }
}