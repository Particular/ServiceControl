namespace ServiceControl.EndpointControl.Handlers
{
    using System.Threading.Tasks;
    using Infrastructure;
    using InternalMessages;
    using NServiceBus;
    using ServiceControl.Contracts.HeartbeatMonitoring;

    class DetectEndpointsFromHeartbeats : IHandleMessages<HeartbeatingEndpointDetected>
    {
        public KnownEndpointsCache KnownEndpointsCache { get; set; }

        public async Task Handle(HeartbeatingEndpointDetected message, IMessageHandlerContext context)
        {
            var id = DeterministicGuid.MakeId(message.Endpoint.Name, message.Endpoint.HostId.ToString());

            if (KnownEndpointsCache.TryAdd(id))
            {
                await context.SendLocal(new RegisterEndpoint
                {
                    EndpointInstanceId = id,
                    Endpoint = message.Endpoint,
                    DetectedAt = message.DetectedAt
                });
            }
        }
    }
}