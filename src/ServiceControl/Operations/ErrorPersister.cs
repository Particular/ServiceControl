namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using BodyStorage;
    using Contracts.Operations;
    using Infrastructure;
    using MessageFailures;
    using Monitoring;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using Raven.Client.Connection;
    using Raven.Client.Document;
    using Raven.Imports.Newtonsoft.Json;
    using Raven.Json.Linq;
    using Recoverability;
    using SagaAudit;
    using JsonSerializer = Raven.Imports.Newtonsoft.Json.JsonSerializer;

    class ErrorPersister
    {
        static ErrorPersister()
        {
            Serializer = JsonExtensions.CreateDefaultJsonSerializer();
            Serializer.TypeNameHandling = TypeNameHandling.Auto;

            JObjectMetadata = RavenJObject.Parse($@"
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

        public async Task<IReadOnlyList<MessageContext>> Persist(List<MessageContext> contexts)
        {
            var stopwatch = Stopwatch.StartNew();

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Batch size {contexts.Count}");
            }

            var storedContexts = new List<MessageContext>(contexts.Count);
            BulkInsertOperation bulkInsert = null;
            try
            {
                // deliberately not using the using statement because we dispose async explicitly
                bulkInsert = store.BulkInsert(options: new BulkInsertOptions
                {
                    OverwriteExisting = true,
                    ChunkedBulkInsertOptions = null,
                    BatchSize = contexts.Count
                });
                var tasks = new List<Task>(contexts.Count);
                foreach (var context in contexts)
                {
                    tasks.Add(ProcessMessage(context));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);

                var commands = new List<ICommandData>(contexts.Count);
                var knownEndpoints = new Dictionary<string, KnownEndpoint>();
                foreach (var context in contexts)
                {
                    if (!context.Extensions.TryGet<ICommandData>(out var command))
                    {
                        continue;
                    }

                    commands.Add(command);
                    storedContexts.Add(context);

                    foreach (var endpointDetail in context.Extensions.Get<IEnumerable<EndpointDetails>>())
                    {
                        RecordKnownEndpoints(endpointDetail, knownEndpoints);
                    }
                }

                // not really interested in the batch results since a batch is atomic
                await bulkInsert.DatabaseCommands.BatchAsync(commands)
                    .ConfigureAwait(false);

                foreach (var endpoint in knownEndpoints.Values)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Adding known endpoint for bulk storage");
                    }
                    await bulkInsert.StoreAsync(endpoint).ConfigureAwait(false);
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
                if (bulkInsert != null)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Performing bulk session dispose");
                    }

                    try
                    {
                        // this can throw even though dispose is never supposed to throw
                        await bulkInsert.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        if (Logger.IsDebugEnabled)
                        {
                            Logger.Debug("Bulk insertion dispose failed", e);
                        }

                        // making sure to rethrow so that all messages get marked as failed
                        throw;
                    }
                }

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
            // TODO optimize
            if (Logger.IsDebugEnabled)
            {
                context.Headers.TryGetValue(Headers.MessageId, out var originalMessageId);
                Logger.Debug($"Ingesting error message {context.MessageId} (original message id: {originalMessageId ?? string.Empty})");
            }

            if (!context.Headers.TryGetValue(Headers.MessageId, out var messageId))
            {
                messageId = DeterministicGuid.MakeId(context.MessageId).ToString();
            }

            try
            {
                var metadata = new ConcurrentDictionary<string, object>
                {
                    ["MessageId"] = messageId,
                    ["MessageIntent"] = context.Headers.MessageIntent()
                };

                var enricherContext = new ErrorEnricherContext(context.Headers, metadata);
                foreach (var enricher in enrichers)
                {
                    enricher.Enrich(enricherContext);
                }

                await bodyStorageEnricher.StoreErrorMessageBody(context.Body, context.Headers, metadata)
                    .ConfigureAwait(false);

                var failureDetails = failedMessageFactory.ParseFailureDetails(context.Headers);

                var processingAttempt = failedMessageFactory.CreateProcessingAttempt(
                    context.Headers,
                    new Dictionary<string, object>(metadata),
                    failureDetails);

                var groups = failedMessageFactory.GetGroups((string)metadata["MessageType"], failureDetails, processingAttempt);

                var patchCommand = CreatePatchCommand(context.Headers.UniqueId(), processingAttempt, groups);

                context.Extensions.Set(patchCommand);
                context.Extensions.Set(failureDetails);
                context.Extensions.Set(enricherContext.NewEndpoints);
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

        ICommandData CreatePatchCommand(string uniqueMessageId, FailedMessage.ProcessingAttempt processingAttempt,
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
                Metadata = JObjectMetadata
            };
        }

        static void RecordKnownEndpoints(EndpointDetails observedEndpoint, Dictionary<string, KnownEndpoint> observedEndpoints)
        {
            var uniqueEndpointId = $"{observedEndpoint.Name}{observedEndpoint.HostId}";
            if (!observedEndpoints.TryGetValue(uniqueEndpointId, out KnownEndpoint _))
            {
                observedEndpoints.Add(uniqueEndpointId, new KnownEndpoint
                {
                    Id = DeterministicGuid.MakeId(observedEndpoint.Name, observedEndpoint.HostId.ToString()),
                    EndpointDetails = observedEndpoint,
                    HostDisplayName = observedEndpoint.Host,
                    Monitored = false
                });
            }
        }

        IEnrichImportedErrorMessages[] enrichers;
        BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;
        FailedMessageFactory failedMessageFactory;
        IDocumentStore store;
        static RavenJObject JObjectMetadata;
        static JsonSerializer Serializer;
        static ILog Logger = LogManager.GetLogger<ErrorPersister>();
    }
}