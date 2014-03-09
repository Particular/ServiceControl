namespace ServiceControl.EndpointControl.Handlers
{
    using System;
    using Contracts.Operations;
    using InternalMessages;
    using NServiceBus;
    using Operations;

    class DetectNewEndpointsFromImportsEnricher : ImportEnricher
    {
        public IBus Bus { get; set; }

        public KnownEndpointsCache KnownEndpointsCache { get; set; }
        
        public override void Enrich(ImportMessage message)
        {
            TryAddEndpoint(EndpointDetails.SendingEndpoint(message.PhysicalMessage.Headers));
            TryAddEndpoint(EndpointDetails.ReceivingEndpoint(message.PhysicalMessage.Headers));
        }

        void TryAddEndpoint(EndpointDetails endpointDetails)
        {
            var id = endpointDetails.Name + endpointDetails.Host;

            if (KnownEndpointsCache.TryAdd(id))
            {
                Bus.SendLocal(new RegisterEndpoint
                {
                    Endpoint = endpointDetails,
                    DetectedAt = DateTime.UtcNow
                });
            }
        }

    }
}