namespace ServiceControl.EndpointControl.Handlers
{
    using Contracts.HeartbeatMonitoring;
    using Contracts.Operations;
    using InternalMessages;
    using NServiceBus;

    class DetectEndpointsFromHeartbeats : IHandleMessages<HeartbeatingEndpointDetected>
    {
        public IBus Bus { get; set; }

        public KnownEndpointsCache KnownEndpointsCache { get; set; }

        public void Handle(HeartbeatingEndpointDetected message)
        {
            var endpointDetails = new EndpointDetails
            {
                Name = message.Endpoint,
                Machine = message.Machine
            };

            var id = endpointDetails.Name + endpointDetails.Machine;

            if (KnownEndpointsCache.TryAdd(id))
            {
                Bus.SendLocal(new RegisterEndpoint
                {
                    Endpoint = endpointDetails,
                    DetectedAt = message.DetectedAt
                });
            }
        }
    }
}