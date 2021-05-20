namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using BodyStorage;
    using Infrastructure;
    using Infrastructure.Metrics;
    using MessageFailures;
    using Monitoring;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Raven.Abstractions.Commands;
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

            FailedMessageMetadata = RavenJObject.Parse($@"
                                    {{
                                        ""Raven-Entity-Name"": ""{FailedMessage.CollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(FailedMessage).AssemblyQualifiedName}""
                                    }}");
        }

        public ErrorPersister(IDocumentStore store, BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher, IEnrichImportedErrorMessages[] enrichers, IFailedMessageEnricher[] failedMessageEnrichers,
            Counter ingestedCounter, Meter bulkInsertDurationMeter)
        {
            this.store = store;
            this.bodyStorageEnricher = bodyStorageEnricher;
            this.enrichers = enrichers;
            this.ingestedCounter = ingestedCounter;
            this.bulkInsertDurationMeter = bulkInsertDurationMeter;
            failedMessageFactory = new FailedMessageFactory(failedMessageEnrichers);
        }

        public async Task<IReadOnlyList<MessageContext>> Persist(List<MessageContext> contexts)
        {
            var stopwatch = Stopwatch.StartNew();

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Batch size {contexts.Count}");
            }

            var storedContexts = new List<MessageContext>(contexts.Count);
            try
            {
                var tasks = new List<Task>(contexts.Count);
                foreach (var context in contexts)
                {
                    tasks.Add(ProcessMessage(context));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);

                var commands = new List<ICommandData>(contexts.Count);
                foreach (var context in contexts)
                {
                    if (!context.Extensions.TryGet<ICommandData>(out var command))
                    {
                        continue;
                    }

                    commands.Add(command);
                    storedContexts.Add(context);
                    ingestedCounter.Mark();
                }

                using (bulkInsertDurationMeter.Measure())
                {
                    // not really interested in the batch results since a batch is atomic
                    await store.AsyncDatabaseCommands.BatchAsync(commands)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Bulk insertion failed", e);
                }

                // making sure to rethrow so that all messages get marked as failed
                throw;
            }
            finally
            {
                stopwatch.Stop();
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Batch size {contexts.Count} took {stopwatch.ElapsedMilliseconds} ms");
                }
            }

            return storedContexts;
        }

        async Task ProcessMessage(MessageContext context)
        {
            bool isOriginalMessageId = true;
            if (!context.Headers.TryGetValue(Headers.MessageId, out var messageId))
            {
                messageId = DeterministicGuid.MakeId(context.MessageId).ToString();
                isOriginalMessageId = false;
            }

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Ingesting error message {context.MessageId} (original message id: {(isOriginalMessageId ? messageId : string.Empty)})");
            }

            try
            {
                var metadata = new Dictionary<string, object>
                {
                    ["MessageId"] = messageId,
                    ["MessageIntent"] = context.Headers.MessageIntent()
                };

                var enricherContext = new ErrorEnricherContext(context.Headers, metadata);
                foreach (var enricher in enrichers)
                {
                    enricher.Enrich(enricherContext);
                }

                var failureDetails = failedMessageFactory.ParseFailureDetails(context.Headers);

                var processingAttempt = failedMessageFactory.CreateProcessingAttempt(
                    context.Headers,
                    new Dictionary<string, object>(metadata),
                    failureDetails);

                await bodyStorageEnricher.StoreErrorMessageBody(context.Body, processingAttempt)
                    .ConfigureAwait(false);

                var groups = failedMessageFactory.GetGroups((string)metadata["MessageType"], failureDetails, processingAttempt);

                var patchCommand = CreateFailedMessagesPatchCommand(context.Headers.UniqueId(), processingAttempt, groups);

                context.Extensions.Set(patchCommand);
                context.Extensions.Set(failureDetails);
            }
            catch (Exception e)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Processing of message '{messageId}' failed.", e);
                }

                context.GetTaskCompletionSource().TrySetException(e);
            }
        }

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

        IEnrichImportedErrorMessages[] enrichers;
        readonly Counter ingestedCounter;
        readonly Meter bulkInsertDurationMeter;
        BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;
        FailedMessageFactory failedMessageFactory;
        IDocumentStore store;
        static RavenJObject FailedMessageMetadata;
        static JsonSerializer Serializer;
        static ILog Logger = LogManager.GetLogger<ErrorPersister>();
    }
}