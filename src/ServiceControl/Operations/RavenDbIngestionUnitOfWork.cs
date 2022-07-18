namespace ServiceControl.Operations
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MessageFailures;
    using Monitoring;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using Raven.Imports.Newtonsoft.Json;
    using Raven.Json.Linq;
    using Recoverability;

    class RavenDbIngestionUnitOfWork : IIngestionUnitOfWork
    {
        readonly IDocumentStore store;
        readonly ConcurrentBag<ICommandData> commands;

        public RavenDbIngestionUnitOfWork(IDocumentStore store)
        {
            this.store = store;
            commands = new ConcurrentBag<ICommandData>();
        }

        public void RecordKnownEndpoint(KnownEndpoint knownEndpoint) =>
            commands.Add(CreateKnownEndpointsPutCommand(knownEndpoint));

        public void RecordFailedProcessingAttempt(
            string uniqueMessageId,
            FailedMessage.ProcessingAttempt processingAttempt,
            List<FailedMessage.FailureGroup> groups)
            => commands.Add(CreateFailedMessagesPatchCommand(uniqueMessageId, processingAttempt, groups));

        public void RecordSuccessfulRetry(string retriedMessageUniqueId)
        {
            var failedMessageDocumentId = FailedMessage.MakeDocumentId(retriedMessageUniqueId);
            var failedMessageRetryDocumentId = FailedMessageRetry.MakeDocumentId(retriedMessageUniqueId);

            commands.Add(new PatchCommandData
            {
                Key = failedMessageDocumentId,
                Patches = new[]
                {
                    new PatchRequest {Type = PatchCommandType.Set, Name = nameof(FailedMessage.Status), Value = (int)FailedMessageStatus.Resolved}
                }
            });

            commands.Add(new DeleteCommandData { Key = failedMessageRetryDocumentId });
        }

        public Task Complete() =>
            // not really interested in the batch results since a batch is atomic
            store.AsyncDatabaseCommands.BatchAsync(commands);

        ICommandData CreateFailedMessagesPatchCommand(string uniqueMessageId, FailedMessage.ProcessingAttempt processingAttempt,
            List<FailedMessage.FailureGroup> groups)
        {
            var documentId = FailedMessage.MakeDocumentId(uniqueMessageId);

            var serializedGroups = RavenJToken.FromObject(groups);
            var serializedAttempt = RavenJToken.FromObject(processingAttempt, Serializer);

            return new ScriptedPatchCommandData
            {
                Key = documentId,
                Patch = new ScriptedPatchRequest
                {
                    Script = $@"this.{nameof(FailedMessage.Status)} = status;
                                this.{nameof(FailedMessage.FailureGroups)} = failureGroups;

                                var duplicateIndex = _.findIndex(this.{nameof(FailedMessage.ProcessingAttempts)}, function(a){{
                                    return a.{nameof(FailedMessage.ProcessingAttempt.AttemptedAt)} === attempt.{nameof(FailedMessage.ProcessingAttempt.AttemptedAt)};
                                }});

                                if(duplicateIndex === -1){{
                                    this.{nameof(FailedMessage.ProcessingAttempts)} = _.union(this.{nameof(FailedMessage.ProcessingAttempts)}, [attempt]);
                                }}",
                    Values = new Dictionary<string, object>
                    {
                        {"status", (int)FailedMessageStatus.Unresolved},
                        {"failureGroups", serializedGroups},
                        {"attempt", serializedAttempt}
                    },
                },
                PatchIfMissing = new ScriptedPatchRequest
                {
                    Script = $@"this.{nameof(FailedMessage.Status)} = status;
                                this.{nameof(FailedMessage.FailureGroups)} = failureGroups;
                                this.{nameof(FailedMessage.ProcessingAttempts)} = [attempt];
                                this.{nameof(FailedMessage.UniqueMessageId)} = uniqueMessageId;
                             ",
                    Values = new Dictionary<string, object>
                    {
                        {"status", (int)FailedMessageStatus.Unresolved},
                        {"failureGroups", serializedGroups},
                        {"attempt", serializedAttempt},
                        {"uniqueMessageId", uniqueMessageId}
                    }
                },
                Metadata = FailedMessageMetadata
            };
        }

        static PutCommandData CreateKnownEndpointsPutCommand(KnownEndpoint endpoint) => new PutCommandData
        {
            Document = RavenJObject.FromObject(endpoint),
            Etag = null,
            Key = endpoint.Id.ToString(),
            Metadata = KnownEndpointMetadata
        };

        static RavenDbIngestionUnitOfWork()
        {
            Serializer = JsonExtensions.CreateDefaultJsonSerializer();
            Serializer.TypeNameHandling = TypeNameHandling.Auto;

            FailedMessageMetadata = RavenJObject.Parse($@"
                                    {{
                                        ""Raven-Entity-Name"": ""{FailedMessage.CollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(FailedMessage).AssemblyQualifiedName}""
                                    }}");

            KnownEndpointMetadata = RavenJObject.Parse($@"
                                    {{
                                        ""Raven-Entity-Name"": ""{KnownEndpoint.CollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(KnownEndpoint).AssemblyQualifiedName}""
                                    }}");
        }

        static readonly RavenJObject FailedMessageMetadata;
        static readonly RavenJObject KnownEndpointMetadata;
        static readonly JsonSerializer Serializer;
    }
}