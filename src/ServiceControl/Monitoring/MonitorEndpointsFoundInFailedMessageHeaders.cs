namespace ServiceControl.EndpointControl.Handlers
{
    using System;
    using System.Collections.Generic;
    using Infrastructure;
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
            var knownEndpoints = new Dictionary<string, KnownEndpoint>();
            foreach (var context in batch)
            {
                var errorEnricherContext = context.Extensions.Get<ErrorEnricherContext>();

                void RecordKnownEndpoint(string key)
                {
                    if (errorEnricherContext.Metadata.TryGetValue(key, out var endpointObject)
                        && endpointObject is EndpointDetails endpointDetails
                        && endpointDetails.HostId != Guid.Empty) // for backwards compat with version before 4_5 we might not have a hostid
                    {
                        if (endpointInstanceMonitoring.IsNewInstance(endpointDetails))
                        {
                            RecordKnownEndpoints(endpointDetails, knownEndpoints);
                        }
                    }
                }

                RecordKnownEndpoint("SendingEndpoint");
                RecordKnownEndpoint("ReceivingEndpoint");
            }

            foreach (var endpoint in knownEndpoints.Values)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Adding known endpoint '{endpoint.EndpointDetails.Name}' for bulk storage");
                }

                commands.Add(CreateKnownEndpointsPutCommand(endpoint));
            }

        }

        static void RecordKnownEndpoints(EndpointDetails observedEndpoint, Dictionary<string, KnownEndpoint> observedEndpoints)
        {
            var uniqueEndpointId = $"{observedEndpoint.Name}{observedEndpoint.HostId}";
            if (!observedEndpoints.TryGetValue(uniqueEndpointId, out KnownEndpoint _))
            {
                observedEndpoints.Add(uniqueEndpointId, new KnownEndpoint
                {
                    Id = DeterministicGuid.MakeId(observedEndpoint.Name, observedEndpoint.HostId.ToString()),
                    EndpointDetails = observedEndpoint,
                    HostDisplayName = observedEndpoint.Host,
                    Monitored = false
                });
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