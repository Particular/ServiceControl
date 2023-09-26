namespace ServiceControl.Persistence.RavenDb
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Transport;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Operations;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Persistence.UnitOfWork;
    using ServiceControl.Recoverability;
    using Sparrow.Json.Parsing;

    class RavenDbRecoverabilityIngestionUnitOfWork : IRecoverabilityIngestionUnitOfWork
    {
        RavenDbIngestionUnitOfWork parentUnitOfWork;

        public RavenDbRecoverabilityIngestionUnitOfWork(RavenDbIngestionUnitOfWork parentUnitOfWork)
        {
            this.parentUnitOfWork = parentUnitOfWork;
        }

        public Task RecordFailedProcessingAttempt(
            MessageContext context,
            FailedMessage.ProcessingAttempt processingAttempt,
            List<FailedMessage.FailureGroup> groups)
        {
            var uniqueMessageId = context.Headers.UniqueId();
            var bodyId = processingAttempt.Headers.MessageId();
            var contentType = GetContentType(context.Headers, "text/xml");
            processingAttempt.MessageMetadata.Add("ContentType", contentType);
            processingAttempt.MessageMetadata.Add("BodyUrl", $"/messages/{bodyId}/body");

            var storeMessageCmd = CreateFailedMessagesPatchCommand(uniqueMessageId, processingAttempt, groups);
            parentUnitOfWork.AddCommand(storeMessageCmd);

            AddStoreBodyCommands(context, contentType);

            return Task.CompletedTask;
        }

        public Task RecordSuccessfulRetry(string retriedMessageUniqueId)
        {
            var failedMessageDocumentId = FailedMessageIdGenerator.MakeDocumentId(retriedMessageUniqueId);
            var failedMessageRetryDocumentId = FailedMessageRetry.MakeDocumentId(retriedMessageUniqueId);

            parentUnitOfWork.AddCommand(new PatchCommandData(failedMessageDocumentId, null, new PatchRequest
            {
                Script = $@"this.{nameof(FailedMessage.Status)} = {(int)FailedMessageStatus.Resolved};"
            }));

            parentUnitOfWork.AddCommand(new DeleteCommandData(failedMessageRetryDocumentId, null));
            return Task.CompletedTask;
        }

        ICommandData CreateFailedMessagesPatchCommand(string uniqueMessageId, FailedMessage.ProcessingAttempt processingAttempt,
            List<FailedMessage.FailureGroup> groups)
        {
            var documentId = FailedMessageIdGenerator.MakeDocumentId(uniqueMessageId);

            //HINT: RavenDB 3.5 is using Lodash v4.13.1 to provide javascript utility functions
            //      https://ravendb.net/docs/article-page/3.5/csharp/client-api/commands/patches/how-to-use-javascript-to-patch-your-documents#methods-objects-and-variables
            return new PatchCommandData(documentId, null, new PatchRequest
            {
                Script = $@"this.{nameof(FailedMessage.Status)} = args.status;
                                this.{nameof(FailedMessage.FailureGroups)} = args.failureGroups;
                                
                                var newAttempts = this.{nameof(FailedMessage.ProcessingAttempts)};

                                //De-duplicate attempts by AttemptedAt value

                                var duplicateIndex = _.findIndex(this.{nameof(FailedMessage.ProcessingAttempts)}, function(a){{
                                    return a.{nameof(FailedMessage.ProcessingAttempt.AttemptedAt)} === attempt.{nameof(FailedMessage.ProcessingAttempt.AttemptedAt)};
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
                        {"failureGroups", groups},
                        {"attempt", processingAttempt}
                    },
            },
                patchIfMissing: new PatchRequest
                {
                    Script = $@"this.{nameof(FailedMessage.Status)} = args.status;
                                this.{nameof(FailedMessage.FailureGroups)} = args.failureGroups;
                                this.{nameof(FailedMessage.ProcessingAttempts)} = [args.attempt];
                                this.{nameof(FailedMessage.UniqueMessageId)} = args.uniqueMessageId;
                                this['@metadata'] = {{
                                    '@collection': '{FailedMessageIdGenerator.CollectionName}',
                                    'Raven-Clr-Type': '{typeof(FailedMessage).AssemblyQualifiedName}'
                                }}
                             ",
                    Values = new Dictionary<string, object>
                    {
                        {"status", (int)FailedMessageStatus.Unresolved},
                        {"failureGroups", groups},
                        {"attempt", processingAttempt},
                        {"uniqueMessageId", uniqueMessageId}
                    }
                });
        }

        void AddStoreBodyCommands(MessageContext context, string contentType)
        {
            var messageId = context.Headers.MessageId();
            var documentId = $"MessageBodies/{messageId}";

            var emptyDoc = new DynamicJsonValue();
            var putOwnerDocumentCmd = new PutCommandData(documentId, null, emptyDoc);

            var stream = Memory.Manager.GetStream(context.Body);
            var putAttachmentCmd = new PutAttachmentCommandData(documentId, "body", stream, contentType, changeVector: null);

            parentUnitOfWork.AddCommand(putOwnerDocumentCmd);
            parentUnitOfWork.AddCommand(putAttachmentCmd);
        }

        static string GetContentType(IReadOnlyDictionary<string, string> headers, string defaultContentType)
        {
            if (!headers.TryGetValue(Headers.ContentType, out var contentType))
            {
                contentType = defaultContentType;
            }

            return contentType;
        }

        static int MaxProcessingAttempts = 10;
    }
}