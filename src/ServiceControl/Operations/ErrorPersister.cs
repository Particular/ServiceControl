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

            bodyStorageEnricher.StoreErrorMessageBody(message.Body, message.Headers, metadata);

            var failureDetails = failedMessageFactory.ParseFailureDetails(message.Headers);

            var processingAttempt = failedMessageFactory.CreateProcessingAttempt(
                message.Headers,
                new Dictionary<string, object>(metadata),
                failureDetails);

            var groups = failedMessageFactory.GetGroups((string)metadata["MessageType"], failureDetails, processingAttempt);

            var uniqueMessageId = message.Headers.UniqueId();
            await SaveToDb(uniqueMessageId, processingAttempt, groups)
                .ConfigureAwait(false);

            if (message.Body.Length > 0)
            {
                using (var stream = Memory.Manager.GetStream(uniqueMessageId, message.Body, 0, message.Body.Length))
                {
                    await store.Operations.SendAsync(
                        new PutAttachmentOperation(
                            FailedMessage.MakeDocumentId(uniqueMessageId),
                            "body",
                            stream,
                            (string)metadata["ContentType"])
                    ).ConfigureAwait(false);
                }
            }
            return failureDetails;
        }

        async Task SaveToDb(string uniqueMessageId, FailedMessage.ProcessingAttempt processingAttempt, List<FailedMessage.FailureGroup> groups)
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

var duplicate = this.{processingAttemptsField}.find(attempt => attempt.{attemptedAtField} === $attempt.{attemptedAtField});
if(typeof duplicate === ""undefined"") {{
    this.{processingAttemptsField}.push($attempt)
}}
",
                    Values = new Dictionary<string, object>
                    {
                        ["status"] = (int)FailedMessageStatus.Unresolved,
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
                        ["status"] = (int)FailedMessageStatus.Unresolved,
                        ["failureGroups"] = groups,
                        ["attempt"] = processingAttempt,
                        ["uniqueMessageId"] = uniqueMessageId
                    }
                },
                skipPatchIfChangeVectorMismatch: false)
            ).ConfigureAwait(false);
        }

        IEnrichImportedErrorMessages[] enrichers;
        BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;
        FailedMessageFactory failedMessageFactory;
        IDocumentStore store;
    }
}