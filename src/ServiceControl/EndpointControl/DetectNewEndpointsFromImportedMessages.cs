namespace ServiceControl.EndpointControl
{
    using System;
    using System.Collections.Concurrent;
    using Contracts.Operations;
    using InternalMessages;
    using NServiceBus;

    public class DetectNewEndpointsFromImportedMessages : IHandleMessages<ImportMessage>
    {
        public IBus Bus { get; set; }
        public void Handle(ImportMessage message)
        {

            TryAddEndpoint(EndpointDetails.SendingEndpoint(message.PhysicalMessage.Headers));
            TryAddEndpoint(EndpointDetails.ReceivingEndpoint(message.PhysicalMessage.Headers));
        }

        void TryAddEndpoint(EndpointDetails endpointDetails)
        {
            var id = endpointDetails.Name + endpointDetails.Machine;

            if (knownEndpointsCache.TryAdd(id, endpointDetails))
            {
                Bus.SendLocal(new RegisterEndpoint
                {
                    Endpoint = endpointDetails,
                    DetectedAt = DateTime.UtcNow
                });
            }

        }

        static readonly ConcurrentDictionary<string, EndpointDetails> knownEndpointsCache = new ConcurrentDictionary<string, EndpointDetails>();
    }
}