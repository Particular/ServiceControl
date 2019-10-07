namespace ServiceControl.Operations
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using BodyStorage;
    using Contracts.Operations;
    using Infrastructure;
    using MessageFailures;
    using NServiceBus;
    using NServiceBus.Transport;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using Raven.Imports.Newtonsoft.Json;
    using Raven.Json.Linq;
    using Recoverability;
    using JsonSerializer = Raven.Imports.Newtonsoft.Json.JsonSerializer;

    class ErrorPersister
    {
        static ErrorPersister()
        {
            Serializer = JsonExtensions.CreateDefaultJsonSerializer();
            Serializer.TypeNameHandling = TypeNameHandling.Auto;

            JObjectMetadata = RavenJObject.Parse($@"
                                    {{
                                        ""Raven-Entity-Name"": ""{FailedMessage.CollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(FailedMessage).AssemblyQualifiedName}""
                                    }}");
        }

        public ErrorPersister(IDocumentStore store, BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher, IEnrichImportedErrorMessages[] enrichers, IFailedMessageEnricher[] failedMessageEnrichers)
        {
            this.store = store;
            this.bodyStorageEnricher = bodyStorageEnricher;
            this.enrichers = enrichers;
            failedMessageFactory = new FailedMessageFactory(failedMessageEnrichers);
        }

        public async Task<FailureDetails> Persist(MessageContext message)
        {
            if (!message.Headers.TryGetValue(Headers.MessageId, out var messageId))
            {
                messageId = DeterministicGuid.MakeId(message.MessageId).ToString();
            }

            var metadata = new ConcurrentDictionary<string, object>
            {
                ["MessageId"] = messageId,
                ["MessageIntent"] = message.Headers.MessageIntent()
            };

            var enricherTasks = new List<Task>(enrichers.Length);
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var enricher in enrichers)
            {
                enricherTasks.Add(enricher.Enrich(message.Headers, metadata));
            }

            await Task.WhenAll(enricherTasks)
                .ConfigureAwait(false);

            await bodyStorageEnricher.StoreErrorMessageBody(message.Body, message.Headers, metadata)
                .ConfigureAwait(false);

            var failureDetails = failedMessageFactory.ParseFailureDetails(message.Headers);

            var processingAttempt = failedMessageFactory.CreateProcessingAttempt(
                message.Headers,
                new Dictionary<string, object>(metadata),
                failureDetails);

            var groups = failedMessageFactory.GetGroups((string)metadata["MessageType"], failureDetails, processingAttempt);

            await SaveToDb(message.Headers.UniqueId(), processingAttempt, groups)
                .ConfigureAwait(false);

            return failureDetails;
        }

        Task SaveToDb(string uniqueMessageId, FailedMessage.ProcessingAttempt processingAttempt, List<FailedMessage.FailureGroup> groups)
        {
            var documentId = FailedMessage.MakeDocumentId(uniqueMessageId);

            return store.AsyncDatabaseCommands.PatchAsync(documentId,
                new[]
                {
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.Status),
                        Type = PatchCommandType.Set,
                        Value = (int)FailedMessageStatus.Unresolved
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
                        Value = (int)FailedMessageStatus.Unresolved
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

        IEnrichImportedErrorMessages[] enrichers;
        BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;
        FailedMessageFactory failedMessageFactory;
        IDocumentStore store;
        static RavenJObject JObjectMetadata;
        static JsonSerializer Serializer;
    }
}