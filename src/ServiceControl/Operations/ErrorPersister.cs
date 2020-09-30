namespace ServiceControl.Operations
{
    using System;
    using BodyStorage;
    using Contracts.Operations;
    using Infrastructure;
    using MessageFailures;
    using NServiceBus;
    using NServiceBus.Transport;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Operations;
    using Recoverability;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Raven.Client.Documents.Operations.Attachments;


    class ErrorPersister
    {
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

            using (var stream = new MemoryStream(message.Body))
            {
                await store.Operations.SendAsync(
                    new PutAttachmentOperation(
                        FailedMessage.MakeDocumentId(message.Headers.UniqueId()),
                        "body",
                        stream,
                        (string)metadata["ContentType"])
                ).ConfigureAwait(false);
            }


            return failureDetails;
        }

        async Task SaveToDb(string uniqueMessageId, FailedMessage.ProcessingAttempt processingAttempt, List<FailedMessage.FailureGroup> groups)
        {
            try
            {
                var documentId = FailedMessage.MakeDocumentId(uniqueMessageId);

                var attemptedAtField = nameof(FailedMessage.ProcessingAttempt.AttemptedAt);
                var processingAttemptsField = nameof(FailedMessage.ProcessingAttempts);
                var statusField = nameof(FailedMessage.Status);
                var failureGroupsField = nameof(FailedMessage.FailureGroups);
                var uniqueMessageIdField = nameof(FailedMessage.UniqueMessageId);

                await store.Operations.SendAsync(new PatchOperation<FailedMessage>(documentId, null,
                    new PatchRequest
                    {
                        Script = $@"
this.{statusField} = $status;
this.{failureGroupsField} = $failureGroups;

var duplicateIndex = _.findIndex(this.{processingAttemptsField}, function(a){{
 return a.{attemptedAtField} === $attempt.{attemptedAtField};
}});

if(duplicateIndex === -1){{
 this.{processingAttemptsField} = _.union(this.{processingAttemptsField}, [$attempt]);
}}
",
                        Values = new Dictionary<string, object>
                        {
                            ["status"] = FailedMessageStatus.Unresolved,
                            ["failureGroups"] = groups,
                            ["attempt"] = processingAttempt,
                        }
                    },
                    new PatchRequest
                    {
                        Script = $@"
this.{statusField} = $status;
this.{failureGroupsField} = $failureGroups;
this.{processingAttemptsField} = [$attempt];
this.{uniqueMessageIdField} = $uniqueMessageId;
this['@metadata'] = {{ '@collection': 'FailedMessages' }} 
",

                        Values = new Dictionary<string, object>
                        {
                            ["status"] = FailedMessageStatus.Unresolved,
                            ["failureGroups"] = groups,
                            ["attempt"] = processingAttempt,
                            ["uniqueMessageId"] = uniqueMessageId
                        }
                    },
                    skipPatchIfChangeVectorMismatch: false)
                ).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // TODO: RAVEN5 - This is there to hang a breakpoint on. It should be removed before we finish.
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        IEnrichImportedErrorMessages[] enrichers;
        BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;
        FailedMessageFactory failedMessageFactory;
        IDocumentStore store;
    }
}