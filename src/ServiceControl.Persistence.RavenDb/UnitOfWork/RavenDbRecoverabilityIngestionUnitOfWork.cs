namespace ServiceControl.Persistence.RavenDb
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MessageFailures;
    using Operations;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Imports.Newtonsoft.Json;
    using Raven.Json.Linq;
    using Recoverability;

    class RavenDbRecoverabilityIngestionUnitOfWork : IRecoverabilityIngestionUnitOfWork
    {
        RavenDbIngestionUnitOfWork parentUnitOfWork;

        public RavenDbRecoverabilityIngestionUnitOfWork(RavenDbIngestionUnitOfWork parentUnitOfWork)
        {
            this.parentUnitOfWork = parentUnitOfWork;
        }

        public Task RecordFailedProcessingAttempt(
            string uniqueMessageId,
            FailedMessage.ProcessingAttempt processingAttempt,
            List<FailedMessage.FailureGroup> groups)
        {
            parentUnitOfWork.AddCommand(
                CreateFailedMessagesPatchCommand(uniqueMessageId, processingAttempt, groups)
            );
            return Task.CompletedTask;
        }

        public Task RecordSuccessfulRetry(string retriedMessageUniqueId)
        {
            var failedMessageDocumentId = FailedMessage.MakeDocumentId(retriedMessageUniqueId);
            var failedMessageRetryDocumentId = FailedMessageRetry.MakeDocumentId(retriedMessageUniqueId);

            parentUnitOfWork.AddCommand(new PatchCommandData
            {
                Key = failedMessageDocumentId,
                Patches = new[]
                {
                    new PatchRequest {Type = PatchCommandType.Set, Name = nameof(FailedMessage.Status), Value = (int)FailedMessageStatus.Resolved}
                }
            });

            parentUnitOfWork.AddCommand(new DeleteCommandData { Key = failedMessageRetryDocumentId });
            return Task.CompletedTask;
        }

        ICommandData CreateFailedMessagesPatchCommand(string uniqueMessageId, FailedMessage.ProcessingAttempt processingAttempt,
            List<FailedMessage.FailureGroup> groups)
        {
            var documentId = FailedMessage.MakeDocumentId(uniqueMessageId);

            var serializedGroups = RavenJToken.FromObject(groups);
            var serializedAttempt = RavenJToken.FromObject(processingAttempt, Serializer);

            //HINT: RavenDB5 is using Lodash v4.13.1 to provide javascript utility functions
            //      https://ravendb.net/docs/article-page/3.5/csharp/client-api/commands/patches/how-to-use-javascript-to-patch-your-documents#methods-objects-and-variables
            return new ScriptedPatchCommandData
            {
                Key = documentId,
                Patch = new ScriptedPatchRequest
                {
                    Script = $@"this.{nameof(FailedMessage.Status)} = status;
                                this.{nameof(FailedMessage.FailureGroups)} = failureGroups;
                                
                                var newAttempts = _.union(this.{nameof(FailedMessage.ProcessingAttempts)}, [attempt]);
                                
                                newAttempts = _.sortBy(newAttempts, function(a) {{
                                    return a.{nameof(FailedMessage.ProcessingAttempt.AttemptedAt)};
                                }});
                                
                                if(newAttempts.length > {MaxProcessingAttempts})
                                {{
                                    newAttempts = _.slice(newAttempts, 1, {MaxProcessingAttempts} + 1); 
                                }}

                                this.{nameof(FailedMessage.ProcessingAttempts)} = newAttempts;
                                ",
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

        static RavenDbRecoverabilityIngestionUnitOfWork()
        {
            Serializer = JsonExtensions.CreateDefaultJsonSerializer();
            Serializer.TypeNameHandling = TypeNameHandling.Auto;

            FailedMessageMetadata = RavenJObject.Parse($@"
                                    {{
                                        ""Raven-Entity-Name"": ""{FailedMessage.CollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(FailedMessage).AssemblyQualifiedName}""
                                    }}");
        }

        static int MaxProcessingAttempts = 10;
        static readonly RavenJObject FailedMessageMetadata;
        static readonly JsonSerializer Serializer;
    }
}