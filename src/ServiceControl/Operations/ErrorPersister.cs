namespace ServiceControl.Operations
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using BodyStorage;
    using Contracts.Operations;
    using Infrastructure;
    using MessageFailures;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NServiceBus.Transport;
    using Raven.Client.Documents;
    using Recoverability;


    class ErrorPersister
    {
        static ErrorPersister()
        {
            //TODO: RAVEN5 Json Serializer
            Serializer = Newtonsoft.Json.JsonSerializer.Create();
            Serializer.TypeNameHandling = TypeNameHandling.Auto;

            JObjectMetadata = JObject.Parse($@"
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
            return Task.FromResult(0);
            //TODO: RAVEN5 missing AsyncDatabaseCommands
            // var documentId = FailedMessage.MakeDocumentId(uniqueMessageId);
            //
            // var attemptedAtField = nameof(FailedMessage.ProcessingAttempt.AttemptedAt);
            // var processingAttemptsField = nameof(FailedMessage.ProcessingAttempts);
            // var statusField = nameof(FailedMessage.Status);
            // var failureGroupsField = nameof(FailedMessage.FailureGroups);
            // var uniqueMessageIdField = nameof(FailedMessage.UniqueMessageId);

//             var serializedGroups = RavenJToken.FromObject(groups);
//             var serializedAttempt = RavenJToken.FromObject(processingAttempt, Serializer);
//
//             return store.AsyncDatabaseCommands.PatchAsync(documentId,
//                 new ScriptedPatchRequest
//                 {
//                     Script = $@"this.{statusField} = status;
//                                 this.{failureGroupsField} = failureGroups;
//
//                                 var duplicateIndex = _.findIndex(this.{processingAttemptsField}, function(a){{
//                                     return a.{attemptedAtField} === attempt.{attemptedAtField};
//                                 }});
//
//                                 if(duplicateIndex === -1){{
//                                     this.{processingAttemptsField} = _.union(this.{processingAttemptsField}, [attempt]);
//                                 }}",
//                     Values = new Dictionary<string, object>
//                     {
//                         { "status", (int)FailedMessageStatus.Unresolved },
//                         { "failureGroups", serializedGroups },
//                         { "attempt", serializedAttempt}
//                     }
//                 },
//                 new ScriptedPatchRequest
//                 {
//                     Script = $@"this.{statusField} = status;
//                                 this.{failureGroupsField} = failureGroups;
//                                 this.{processingAttemptsField} = [attempt];
//                                 this.{uniqueMessageIdField} = uniqueMessageId;
//                              ",
//                     Values = new Dictionary<string, object>
//                     {
//                         { "status", (int)FailedMessageStatus.Unresolved },
//                         { "failureGroups", serializedGroups },
//                         { "attempt", serializedAttempt},
//                         { "uniqueMessageId", uniqueMessageId }
//                     }
//                 },
//                 JObjectMetadata);
        }

        IEnrichImportedErrorMessages[] enrichers;
        BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;
        FailedMessageFactory failedMessageFactory;
        IDocumentStore store;
        static JObject JObjectMetadata;
        static JsonSerializer Serializer;
    }
}