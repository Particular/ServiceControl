namespace ServiceControl.EndpointControl.Handlers
{
    using System;
    using System.Collections.Generic;
    using Monitoring;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Operations;
    using Raven.Abstractions.Commands;
    using Raven.Json.Linq;
    using ServiceControl.Contracts.Operations;

    class MonitorEndpointsFoundInFailedMessageHeaders : IErrorMessageBatchPlugin
    {
        public void AfterProcessing(List<MessageContext> batch, ICollection<ICommandData> commands)
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

            foreach (var endpoint in endpointInstanceMonitoring.DetectEndpointsFromBulkIngestion(observedEndpoints))
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Adding known endpoint '{endpoint.EndpointDetails.Name}' for bulk storage");
                }

                commands.Add(CreateKnownEndpointsPutCommand(endpoint));
            }

        }

        static PutCommandData CreateKnownEndpointsPutCommand(KnownEndpoint endpoint) =>
            new PutCommandData
            {
                Document = RavenJObject.FromObject(endpoint),
                Etag = null,
                Key = endpoint.Id.ToString(),
                Metadata = KnownEndpointMetadata
            };


        public MonitorEndpointsFoundInFailedMessageHeaders(EndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            this.endpointInstanceMonitoring = endpointInstanceMonitoring;
        }

        static MonitorEndpointsFoundInFailedMessageHeaders()
        {
            KnownEndpointMetadata = RavenJObject.Parse($@"
                                    {{
                                        ""Raven-Entity-Name"": ""{KnownEndpoint.CollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(KnownEndpoint).AssemblyQualifiedName}""
                                    }}");
        }

        EndpointInstanceMonitoring endpointInstanceMonitoring;
        static readonly RavenJObject KnownEndpointMetadata;
        static readonly ILog Logger = LogManager.GetLogger<MonitorEndpointsFoundInFailedMessageHeaders>();
    }
}