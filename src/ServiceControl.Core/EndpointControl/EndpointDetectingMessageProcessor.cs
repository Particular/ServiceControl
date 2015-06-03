namespace ServiceControl.EndpointControl
{
    using System;
    using Infrastructure;
    using InternalMessages;
    using NServiceBus;
    using Particular.Operations.Ingestion.Api;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.EndpointControl.Handlers;

    class EndpointDetectingMessageProcessor : IProcessSuccessfulMessages, IProcessFailedMessages
    {
        readonly IBus bus;
        readonly KnownEndpointsCache knownEndpointsCache;

        public EndpointDetectingMessageProcessor(IBus bus, KnownEndpointsCache knownEndpointsCache)
        {
            this.bus = bus;
            this.knownEndpointsCache = knownEndpointsCache;
        }

        void Detect(IngestedMessage message)
        {
            // SendingEndpoint will be unknown for messages that are from v3.3.x endpoints because we don't
            // have the relevant information via the headers, which were added in v4.
            if (message.SentFrom != EndpointInstance.Unknown)
            {
                TryAddEndpoint(message.SentFrom);
            }

            TryAddEndpoint(message.ProcessedAt);
        }

        void TryAddEndpoint(EndpointInstance endpointDetails)
        {
            if (endpointDetails.HostId == null)
            {
                HandlePre45Endpoint(endpointDetails);
                return;
            }

            HandlePost45Endpoint(endpointDetails);
        }

        void HandlePost45Endpoint(EndpointInstance endpointInstance)
        {
            var endpointInstanceId = DeterministicGuid.MakeId(endpointInstance.EndpointName, endpointInstance.HostId);
            if (knownEndpointsCache.TryAdd(endpointInstanceId))
            {
                var registerEndpoint = new RegisterEndpoint
                {
                    EndpointInstanceId = endpointInstanceId,
                    Endpoint = new EndpointDetails
                    {
                        Name = endpointInstance.EndpointName,
                        HostId = endpointInstance.HostId
                    },
                    DetectedAt = DateTime.UtcNow
                };
                bus.SendLocal(registerEndpoint);
            }
        }

        void HandlePre45Endpoint(EndpointInstance endpointInstance)
        {
            //since for pre 4.5 endpoints we wont have a hostid then fake one
            var endpointInstanceId = DeterministicGuid.MakeId(endpointInstance.EndpointName, endpointInstance.HostId);
            if (knownEndpointsCache.TryAdd(endpointInstanceId))
            {
                var registerEndpoint = new RegisterEndpoint
                {
                    //we don't set then endpoint instance id since we don't have the host id
                    Endpoint = new EndpointDetails
                    {
                        Name = endpointInstance.EndpointName,
                        HostId = endpointInstance.HostId
                    },
                    DetectedAt = DateTime.UtcNow
                };
                bus.SendLocal(registerEndpoint);
            }
        }

        public void ProcessSuccessful(IngestedMessage message)
        {
            Detect(message);
        }

        public void ProcessFailed(IngestedMessage message)
        {
            Detect(message);
        }
    }
}