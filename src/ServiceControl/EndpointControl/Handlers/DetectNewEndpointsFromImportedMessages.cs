namespace ServiceControl.EndpointControl.Handlers
{
    using System;
    using Contracts.Operations;
    using InternalMessages;
    using NServiceBus;

    class DetectNewEndpointsFromImportedMessages : IHandleMessages<ImportMessage>
    {
        public IBus Bus { get; set; }

        public KnownEndpointsCache KnownEndpointsCache { get; set; }
        
        public void Handle(ImportMessage message)
        {
            TryAddEndpoint(EndpointDetails.SendingEndpoint(message.PhysicalMessage.Headers));
            TryAddEndpoint(EndpointDetails.ReceivingEndpoint(message.PhysicalMessage.Headers));
        }

        void TryAddEndpoint(EndpointDetails endpointDetails)
        {
            var id = endpointDetails.Name + endpointDetails.Machine;

            if (KnownEndpointsCache.Endpoints.TryAdd(id, endpointDetails))
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