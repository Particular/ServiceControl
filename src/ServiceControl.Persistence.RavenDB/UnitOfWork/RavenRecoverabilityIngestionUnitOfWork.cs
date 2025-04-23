namespace ServiceControl.Persistence.RavenDB
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

    class RavenRecoverabilityIngestionUnitOfWork(RavenIngestionUnitOfWork parentUnitOfWork, ExpirationManager expirationManager, RavenPersisterSettings settings)
        : IRecoverabilityIngestionUnitOfWork
    {
        public Task RecordFailedProcessingAttempt(
            MessageContext context,
            FailedMessage.ProcessingAttempt processingAttempt,
            List<FailedMessage.FailureGroup> groups)
        {
            var uniqueMessageId = GetUniqueMessageId(context);
            var contentType = GetContentType(context.Headers, "text/xml");
            var bodySize = context.Body.Length;

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
                        var bodyString = utf8.GetString(context.Body.Span);
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

            var patchRequest = new PatchRequest
            {
                Script = $@"this.{nameof(FailedMessage.Status)} = {(int)FailedMessageStatus.Resolved};"
            };

            expirationManager.EnableExpiration(patchRequest);

            parentUnitOfWork.AddCommand(new PatchCommandData(failedMessageDocumentId, null, patchRequest));

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
            var uniqueId = GetUniqueMessageId(context);
            var documentId = FailedMessageIdGenerator.MakeDocumentId(uniqueId);

            var stream = new ReadOnlyStream(context.Body);
            var putAttachmentCmd = new PutAttachmentCommandData(documentId, "body", stream, contentType, changeVector: null);

            parentUnitOfWork.AddCommand(putAttachmentCmd);
        }

        static string GetUniqueMessageId(MessageContext context)
        {
            var headers = context.Headers;

            if (headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out var existingUniqueMessageId))
            {
                return existingUniqueMessageId;
            }

            if (!headers.TryGetValue(Headers.MessageId, out var messageId))
            {
                messageId = context.NativeMessageId;
            }

            return DeterministicGuid.MakeId(messageId, headers.ProcessingEndpointName()).ToString();
        }

        readonly bool doFullTextIndexing = settings.EnableFullTextSearchOnBodies;

        static string GetContentType(IReadOnlyDictionary<string, string> headers, string defaultContentType)
            => headers.GetValueOrDefault(Headers.ContentType, defaultContentType);

        static readonly int MaxProcessingAttempts = 10;
        // large object heap starts above 85000 bytes and not above 85 KB!
        const int LargeObjectHeapThreshold = 85_000;
        static readonly Encoding utf8 = new UTF8Encoding(true, true);

    }
}