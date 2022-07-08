namespace ServiceControl.EndpointControl.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Monitoring;
    using NServiceBus.Transport;
    using Operations;
    using ServiceControl.Contracts.Operations;

    class MonitorEndpointsFoundInFailedMessageHeaders : IErrorMessageBatchPlugin
    {
        public Task AfterProcessing(List<MessageContext> batch)
        {
            var observedEndpoints = new HashSet<EndpointDetails>();
            foreach (var context in batch)
            {
                var errorEnricherContext = context.Extensions.Get<ErrorEnricherContext>();

                void RecordKnownEndpoint(string key)
                {
                    if (errorEnricherContext.Metadata.TryGetValue(key, out var endpointObject)
                        && endpointObject is EndpointDetails endpointDetails
                        && endpointDetails.HostId != Guid.Empty) // for backwards compat with version before 4_5 we might not have a hostid
                    {
                        observedEndpoints.Add(endpointDetails);
                    }
                }

                RecordKnownEndpoint("SendingEndpoint");
                RecordKnownEndpoint("ReceivingEndpoint");
            }

            return endpointInstanceMonitoring.DetectEndpointsFromBulkIngestion(observedEndpoints);
        }

        public MonitorEndpointsFoundInFailedMessageHeaders(EndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            this.endpointInstanceMonitoring = endpointInstanceMonitoring;
        }

        EndpointInstanceMonitoring endpointInstanceMonitoring;
    }
}