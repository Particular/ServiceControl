namespace ServiceControl.Persistence.RavenDb
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence.UnitOfWork;
    using ServiceControl.Recoverability;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Operations;

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
            var failedMessageDocumentId = FailedMessageIdGenerator.MakeDocumentId(retriedMessageUniqueId);
            var failedMessageRetryDocumentId = FailedMessageRetry.MakeDocumentId(retriedMessageUniqueId);

            parentUnitOfWork.AddCommand(new PatchCommandData(failedMessageDocumentId, null, new PatchRequest
            {
                Script = $@"this.nameof(FailedMessage.Status) = {(int)FailedMessageStatus.Resolved};"
            }));

            parentUnitOfWork.AddCommand(new DeleteCommandData(failedMessageRetryDocumentId, null));
            return Task.CompletedTask;
        }

        ICommandData CreateFailedMessagesPatchCommand(string uniqueMessageId, FailedMessage.ProcessingAttempt processingAttempt,
            List<FailedMessage.FailureGroup> groups)
        {
            var documentId = FailedMessageIdGenerator.MakeDocumentId(uniqueMessageId);

            var serializedGroups = JToken.FromObject(groups);
            var serializedAttempt = JToken.FromObject(processingAttempt, Serializer);

            //HINT: RavenDB 3.5 is using Lodash v4.13.1 to provide javascript utility functions
            //      https://ravendb.net/docs/article-page/3.5/csharp/client-api/commands/patches/how-to-use-javascript-to-patch-your-documents#methods-objects-and-variables
            return new PatchCommandData(documentId, null, new PatchRequest
            {
                Script = $@"this.{nameof(FailedMessage.Status)} = args.status;
                                this.{nameof(FailedMessage.FailureGroups)} = args.failureGroups;
                                
                                var newAttempts = this.{nameof(FailedMessage.ProcessingAttempts)};

                                //De-duplicate attempts by AttemptedAt value

                                var duplicateIndex = _.findIndex(this.{nameof(FailedMessage.ProcessingAttempts)}, function(a){{
                                    return a.{nameof(FailedMessage.ProcessingAttempt.AttemptedAt)} === args.attempt.{nameof(FailedMessage.ProcessingAttempt.AttemptedAt)};
                                }});

                                if(duplicateIndex === -1){{
                                    newAttempts = _.union(newAttempts, [args.attempt]);
                                }}

                                //Trim to the latest MaxProcessingAttempts 
                                
                                newAttempts = _.sortBy(newAttempts, function(a) {{
                                    return a.{nameof(FailedMessage.ProcessingAttempt.AttemptedAt)};
                                }});
                                
                                if(newAttempts.length > {MaxProcessingAttempts})
                                {{
                                    newAttempts = _.slice(newAttempts, newAttempts.length - {MaxProcessingAttempts}, newAttempts.length); 
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
                patchIfMissing: new PatchRequest
                {
                    Script = $@"this.{nameof(FailedMessage.Status)} = args.status;
                                this.{nameof(FailedMessage.FailureGroups)} = args.failureGroups;
                                this.{nameof(FailedMessage.ProcessingAttempts)} = [args.attempt];
                                this.{nameof(FailedMessage.UniqueMessageId)} = args.uniqueMessageId;
                                this['@metadata'] = {FailedMessageMetadata}
                             ",
                    Values = new Dictionary<string, object>
                    {
                        {"status", (int)FailedMessageStatus.Unresolved},
                        {"failureGroups", serializedGroups},
                        {"attempt", serializedAttempt},
                        {"uniqueMessageId", uniqueMessageId}
                    }
                });
        }

        static RavenDbRecoverabilityIngestionUnitOfWork()
        {
            //TODO: check if this actually works
            Serializer = JsonSerializer.CreateDefault();
            Serializer.TypeNameHandling = TypeNameHandling.Auto;

            FailedMessageMetadata = JObject.Parse($@"
                                    {{
                                        ""@collection"": ""{FailedMessageIdGenerator.CollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(FailedMessage).AssemblyQualifiedName}""
                                    }}");
        }

        static int MaxProcessingAttempts = 10;
        static readonly JObject FailedMessageMetadata;
        static readonly JsonSerializer Serializer;
    }
}