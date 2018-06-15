namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.Transport;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using Raven.Imports.Newtonsoft.Json;
    using Raven.Json.Linq;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Recoverability;
    using JsonSerializer = Raven.Imports.Newtonsoft.Json.JsonSerializer;

    class FailedMessagePersister
    {
        static RavenJObject JObjectMetadata;
        static JsonSerializer Serializer;

        static FailedMessagePersister()
        {
            Serializer = JsonExtensions.CreateDefaultJsonSerializer();
            Serializer.TypeNameHandling = TypeNameHandling.Auto;

            JObjectMetadata = RavenJObject.Parse($@"
                                    {{
                                        ""Raven-Entity-Name"": ""{FailedMessage.CollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(FailedMessage).AssemblyQualifiedName}""
                                    }}");
        }

        private IEnrichImportedMessages[] enrichers;
        private BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;
        private FailedMessageFactory failedMessageFactory;
        private IDocumentStore store;

        public FailedMessagePersister(IDocumentStore store, BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher, IEnrichImportedMessages[] enrichers, IFailedMessageEnricher[] failedMessageEnrichers)
        {
            this.store = store;
            this.bodyStorageEnricher = bodyStorageEnricher;
            this.enrichers = enrichers.Where(x => x.EnrichErrors).ToArray();
            this.failedMessageFactory = new FailedMessageFactory(failedMessageEnrichers);
        }

        public async Task<FailureDetails> Persist(MessageContext message)
        {
            var metadata = new Dictionary<string, object>
            {
                ["MessageId"] = message.MessageId,
                ["MessageIntent"] = message.Headers.MessageIntent(),
                ["HeadersForSearching"] = string.Join(" ", message.Headers.Values)
            };

            foreach (var enricher in enrichers)
            {
                enricher.Enrich(message.Headers, metadata);
            }

            bodyStorageEnricher.StoreErrorMessageBody(
                message.Body, 
                message.Headers, 
                metadata);

            var failureDetails = failedMessageFactory.ParseFailureDetails(message.Headers);

            var processingAttempt = failedMessageFactory.CreateProcessingAttempt(
                message.Headers,
                metadata,
                failureDetails,
                // TODO: Do we need to persist all of these things separately still?
                message.Headers.MessageIntent(),
                true,
                message.Headers.CorrelationId(),
                message.Headers.ReplyToAddress());

            var groups = failedMessageFactory.GetGroups((string)metadata["MessageType"], failureDetails, processingAttempt);

            await SaveToDb(message.Headers.UniqueId(), processingAttempt, groups)
                .ConfigureAwait(false);

            return failureDetails;
        }

        private Task SaveToDb(string uniqueMessageId, FailedMessage.ProcessingAttempt processingAttempt, List<FailedMessage.FailureGroup> groups)
        {
            var documentId = FailedMessage.MakeDocumentId(uniqueMessageId);

            return store.AsyncDatabaseCommands.PatchAsync(documentId,
                new[]
                {
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.Status),
                        Type = PatchCommandType.Set,
                        Value = (int) FailedMessageStatus.Unresolved
                    },
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.ProcessingAttempts),
                        Type = PatchCommandType.Add,
                        Value = RavenJToken.FromObject(processingAttempt, Serializer) // Need to specify serializer here because otherwise the $type for EndpointDetails is missing and this causes EventDispatcher to blow up!
                    },
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.FailureGroups),
                        Type = PatchCommandType.Set,
                        Value = RavenJToken.FromObject(groups)
                    }
                },
                new[]
                {
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.UniqueMessageId),
                        Type = PatchCommandType.Set,
                        Value = uniqueMessageId
                    },
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.Status),
                        Type = PatchCommandType.Set,
                        Value = (int) FailedMessageStatus.Unresolved
                    },
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.ProcessingAttempts),
                        Type = PatchCommandType.Add,
                        Value = RavenJToken.FromObject(processingAttempt, Serializer) // Need to specify serilaizer here because otherwise the $type for EndpointDetails is missing and this causes EventDispatcher to blow up!
                    },
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.FailureGroups),
                        Type = PatchCommandType.Set,
                        Value = RavenJToken.FromObject(groups)
                    }
                }, JObjectMetadata
            );
        }
    }
}