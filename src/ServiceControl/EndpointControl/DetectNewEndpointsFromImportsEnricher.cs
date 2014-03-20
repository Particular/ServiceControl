namespace ServiceControl.EndpointControl.Handlers
{
    using System;
    using Infrastructure;
    using InternalMessages;
    using NServiceBus;
    using Operations;
    using ServiceControl.Contracts.Operations;

    class DetectNewEndpointsFromImportsEnricher : ImportEnricher
    {
        public IBus Bus { get; set; }

        public KnownEndpointsCache KnownEndpointsCache { get; set; }
        
        public override void Enrich(ImportMessage message)
        {
            TryAddEndpoint(EndpointDetailsParser.SendingEndpoint(message.PhysicalMessage.Headers));
            TryAddEndpoint(EndpointDetailsParser.ReceivingEndpoint(message.PhysicalMessage.Headers));
        }

        void TryAddEndpoint(EndpointDetails endpointDetails)
        {
            Guid id;

            if (endpointDetails.HostId == Guid.Empty)
            {
                id = DeterministicGuid.MakeId(endpointDetails.Name, endpointDetails.Host);
            }
            else
            {
                id = DeterministicGuid.MakeId(endpointDetails.Name, endpointDetails.HostId.ToString());
            }

            if (KnownEndpointsCache.TryAdd(id))
            {
                Bus.SendLocal(new RegisterEndpoint
                {
                    //we don't set then endpoint instance id since we don't have the host id
                    Endpoint = endpointDetails,
                    DetectedAt = DateTime.UtcNow
                });
            }
        }

    }
}