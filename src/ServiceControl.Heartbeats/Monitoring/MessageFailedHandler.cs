namespace ServiceControl.Heartbeats.Monitoring
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Contracts.Operations;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using NServiceBus.Logging;
    using Raven.Abstractions.Commands;
    using Raven.Client;
    using Raven.Json.Linq;
    using ServiceControl.Monitoring;

    class MessageFailedHandler : IDomainHandler<MessageFailed>
    {
        readonly IDocumentStore documentStore;
        readonly EndpointInstanceMonitoring monitor;

        public MessageFailedHandler(IDocumentStore documentStore, EndpointInstanceMonitoring monitor)
        {
            this.documentStore = documentStore;
            this.monitor = monitor;

            KnownEndpointMetadata = RavenJObject.Parse($@"
                                    {{
                                        ""Raven-Entity-Name"": ""{KnownEndpoint.CollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(KnownEndpoint).AssemblyQualifiedName}""
                                    }}");
        }

        public async Task Handle(MessageFailed messageFailed)
        {
            var commands = new List<PutCommandData>();

            TryAddKnownEndpointsPutCommand(messageFailed.SendingEndpoint, commands);
            TryAddKnownEndpointsPutCommand(messageFailed.ReceivingEndpoint, commands);

            await documentStore.AsyncDatabaseCommands.BatchAsync(commands)
                .ConfigureAwait(false);
        }

        void TryAddKnownEndpointsPutCommand(EndpointDetails endpointDetails, List<PutCommandData> commands)
        {
            if (endpointDetails == null)
            {
                return;
            }

            if (monitor.IsNewInstance(endpointDetails))
            {
                var knownEndpoint = CreateKnownEndpoints(endpointDetails);
                var command = CreateKnownEndpointsPutCommand(knownEndpoint);

                commands.Add(command);

                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Adding known endpoint '{knownEndpoint.EndpointDetails.Name}.'");
                }
            }
        }

        static KnownEndpoint CreateKnownEndpoints(EndpointDetails observedEndpoint) =>
            new KnownEndpoint
            {
                Id = DeterministicGuid.MakeId(observedEndpoint.Name, observedEndpoint.HostId.ToString()),
                EndpointDetails = observedEndpoint,
                HostDisplayName = observedEndpoint.Host,
                Monitored = false
            };

        PutCommandData CreateKnownEndpointsPutCommand(KnownEndpoint endpoint) =>
            new PutCommandData
            {
                Document = RavenJObject.FromObject(endpoint),
                Etag = null,
                Key = endpoint.Id.ToString(),
                Metadata = KnownEndpointMetadata
            };

        static RavenJObject KnownEndpointMetadata;
        static ILog Logger = LogManager.GetLogger<MessageFailedHandler>();
    }
}