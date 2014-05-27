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
            var sendingEndpoint = EndpointDetailsParser.SendingEndpoint(message.PhysicalMessage.Headers);

            // SendingEndpoint will be null for messages that are from v3.3.x endpoints because we don't
            // have the relevant information via the headers, which were added in v4.
            if (sendingEndpoint != null)
            {
                TryAddEndpoint(sendingEndpoint);
            }

            var receivingEndpoint = EndpointDetailsParser.ReceivingEndpoint(message.PhysicalMessage.Headers);
            TryAddEndpoint(receivingEndpoint);
        }

        void TryAddEndpoint(EndpointDetails endpointDetails)
        {
            // SendingEndpoint will be null for messages that are from v3.3.x endpoints because we don't
            // have the relevant information via the headers, which were added in v4.
            // The ReceivingEndpoint will be null for messages from v3.3.x endpoints that were successfully
            // processed because we dont have the information from the relevant headers.
            if (endpointDetails == null)
            {
                return;
            }

            // for backwards compat with version before 4_5 we might not have a hostid
            if (endpointDetails.HostId == Guid.Empty)
            {
                HandlePre45Endpoint(endpointDetails);
                return;
            }

            HandlePost45Endpoint(endpointDetails);
        }

        void HandlePost45Endpoint(EndpointDetails endpointDetails)
        {
            var endpointInstanceId = DeterministicGuid.MakeId(endpointDetails.Name, endpointDetails.HostId.ToString());
            if (KnownEndpointsCache.TryAdd(endpointInstanceId))
            {
                var registerEndpoint = new RegisterEndpoint
                {
                    EndpointInstanceId = endpointInstanceId,
                    Endpoint = endpointDetails,
                    DetectedAt = DateTime.UtcNow
                };
                Bus.SendLocal(registerEndpoint);
            }
        }

        void HandlePre45Endpoint(EndpointDetails endpointDetails)
        {
            //since for pre 4.5 endpoints we wont have a hostid then fake one
            var endpointInstanceId = DeterministicGuid.MakeId(endpointDetails.Name, endpointDetails.Host);
            if (KnownEndpointsCache.TryAdd(endpointInstanceId))
            {
                var registerEndpoint = new RegisterEndpoint
                {
                    //we don't set then endpoint instance id since we don't have the host id
                    Endpoint = endpointDetails,
                    DetectedAt = DateTime.UtcNow
                };
                Bus.SendLocal(registerEndpoint);
            }
        }
    }
}