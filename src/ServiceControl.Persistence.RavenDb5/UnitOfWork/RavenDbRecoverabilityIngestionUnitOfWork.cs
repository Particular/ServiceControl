namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Text;
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

    class RavenDbRecoverabilityIngestionUnitOfWork : IRecoverabilityIngestionUnitOfWork
    {
        readonly RavenDbIngestionUnitOfWork parentUnitOfWork;
        readonly bool doFullTextIndexing;
        readonly TimeSpan errorRetentionPeriod;

        public RavenDbRecoverabilityIngestionUnitOfWork(
            RavenDbIngestionUnitOfWork parentUnitOfWork,
            RavenDBPersisterSettings settings
            )
        {
            this.parentUnitOfWork = parentUnitOfWork;
            doFullTextIndexing = settings.EnableFullTextSearchOnBodies;
            errorRetentionPeriod = settings.ErrorRetentionPeriod;
        }

        public Task RecordFailedProcessingAttempt(
            MessageContext context,
            FailedMessage.ProcessingAttempt processingAttempt,
            List<FailedMessage.FailureGroup> groups)
        {
            var uniqueMessageId = context.Headers.UniqueId();
            var contentType = GetContentType(context.Headers, "text/xml");
            var bodySize = context.Body?.Length ?? 0;

            processingAttempt.MessageMetadata.Add("ContentType", contentType);
            processingAttempt.MessageMetadata.Add("ContentLength", bodySize);
            processingAttempt.MessageMetadata.Add("BodyUrl", $"/messages/{uniqueMessageId}/body");

            if (doFullTextIndexing)
            {
                var avoidsLargeObjectHeap = bodySize < LargeObjectHeapThreshold;
                var isBinary = processingAttempt.Headers.IsBinary();
                if (avoidsLargeObjectHeap && !isBinary)
                {
                    try
                    {
                        var bodyString = utf8.GetString(context.Body);
                        processingAttempt.MessageMetadata.Add("MsgFullText", bodyString);
                    }
                    catch (ArgumentException)
                    {
                        // If it won't decode to text, don't index it
                    }
                }
            }

            var storeMessageCmd = CreateFailedMessagesPatchCommand(uniqueMessageId, processingAttempt, groups);
            parentUnitOfWork.AddCommand(storeMessageCmd);

            AddStoreBodyCommands(context, contentType);

            return Task.CompletedTask;
        }

        public Task RecordSuccessfulRetry(string retriedMessageUniqueId)
        {
            var failedMessageDocumentId = FailedMessageIdGenerator.MakeDocumentId(retriedMessageUniqueId);
            var failedMessageRetryDocumentId = FailedMessageRetry.MakeDocumentId(retriedMessageUniqueId);

            var expiredAt = DateTime.UtcNow + errorRetentionPeriod;

            parentUnitOfWork.AddCommand(new PatchCommandData(failedMessageDocumentId, null, new PatchRequest
            {
                Script = $@"this.{nameof(FailedMessage.Status)} = {(int)FailedMessageStatus.Resolved}; this['@expires'] = '{expiredAt:o}';"
            }));

            parentUnitOfWork.AddCommand(new DeleteCommandData(failedMessageRetryDocumentId, null));
            return Task.CompletedTask;
        }

        ICommandData CreateFailedMessagesPatchCommand(string uniqueMessageId, FailedMessage.ProcessingAttempt processingAttempt,
            List<FailedMessage.FailureGroup> groups)
        {
            var documentId = FailedMessageIdGenerator.MakeDocumentId(uniqueMessageId);

            const string ProcessingAttempts = nameof(FailedMessage.ProcessingAttempts);
            const string AttemptedAt = nameof(FailedMessage.ProcessingAttempt.AttemptedAt);

            //HINT: RavenDB 4.2 removed Lodash utility functions, but supports ECMAScript 5.1 and some 6.0 features like arrow functions and array primitive functions
            return new PatchCommandData(documentId, null, new PatchRequest
            {
                Script = $@"this.{nameof(FailedMessage.Status)} = args.status;
                                this.{nameof(FailedMessage.FailureGroups)} = args.failureGroups;
                                
                                var newAttempts = this.{nameof(FailedMessage.ProcessingAttempts)};

                                //De-duplicate attempts by AttemptedAt value
                                var duplicateIndex = this.{ProcessingAttempts}.findIndex(a => a.{AttemptedAt} === args.attempt.{AttemptedAt});

                                if(duplicateIndex < 0){{
                                    newAttempts.push(args.attempt);
                                }}

                                //Trim to the latest MaxProcessingAttempts
                                newAttempts.sort((a, b) => a.{AttemptedAt} > b.{AttemptedAt} ? 1 : -1);
                                
                                if(newAttempts.length > {MaxProcessingAttempts})
                                {{
                                    newAttempts = newAttempts.slice(newAttempts.length - {MaxProcessingAttempts}, newAttempts.length);
                                }}

                                this.{ProcessingAttempts} = newAttempts;
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
            var uniqueId = context.Headers.UniqueId();
            var documentId = FailedMessageIdGenerator.MakeDocumentId(uniqueId);

            var stream = Memory.Manager.GetStream(context.Body);
            var putAttachmentCmd = new PutAttachmentCommandData(documentId, "body", stream, contentType, changeVector: null);

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
        // large object heap starts above 85000 bytes and not above 85 KB!
        internal const int LargeObjectHeapThreshold = 85_000;
        static readonly Encoding utf8 = new UTF8Encoding(true, true);

    }
}