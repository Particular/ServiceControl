﻿namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using BodyStorage;
    using Contracts.MessageFailures;
    using Contracts.Operations;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using Infrastructure.Metrics;
    using MessageFailures;
    using Monitoring;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Imports.Newtonsoft.Json;
    using Raven.Json.Linq;
    using Recoverability;
    using JsonSerializer = Raven.Imports.Newtonsoft.Json.JsonSerializer;

    class ErrorProcessor
    {
        static ErrorProcessor()
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

        public ErrorProcessor(BodyStorageEnricher bodyStorageEnricher, IEnrichImportedErrorMessages[] enrichers, IFailedMessageEnricher[] failedMessageEnrichers, IDomainEvents domainEvents,
            Counter ingestedCounter)
        {
            this.bodyStorageEnricher = bodyStorageEnricher;
            this.enrichers = enrichers;
            this.domainEvents = domainEvents;
            this.ingestedCounter = ingestedCounter;
            failedMessageFactory = new FailedMessageFactory(failedMessageEnrichers);
        }

        public async Task<(IReadOnlyList<MessageContext>, IReadOnlyCollection<ICommandData>)> Process(List<MessageContext> contexts)
        {
            var storedContexts = new List<MessageContext>(contexts.Count);
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
                ingestedCounter.Mark();

                foreach (var endpointDetail in context.Extensions.Get<IEnumerable<EndpointDetails>>())
                {
                    RecordKnownEndpoints(endpointDetail, knownEndpoints);
                }
            }

            foreach (var endpoint in knownEndpoints.Values)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Adding known endpoint '{endpoint.EndpointDetails.Name}' for bulk storage");
                }

                commands.Add(CreateKnownEndpointsPutCommand(endpoint));
            }

            return (storedContexts, commands);
        }

        public Task Announce(MessageContext messageContext)
        {
            var failureDetails = messageContext.Extensions.Get<FailureDetails>();
            var headers = messageContext.Headers;

            var failingEndpointId = headers.ProcessingEndpointName();

            if (headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out var failedMessageId))
            {
                return domainEvents.Raise(new MessageFailed
                {
                    FailureDetails = failureDetails,
                    EndpointId = failingEndpointId,
                    FailedMessageId = failedMessageId,
                    RepeatedFailure = true
                });
            }

            return domainEvents.Raise(new MessageFailed
            {
                FailureDetails = failureDetails,
                EndpointId = failingEndpointId,
                FailedMessageId = headers.UniqueId()
            });
        }

        static PutCommandData CreateKnownEndpointsPutCommand(KnownEndpoint endpoint) =>
            new PutCommandData
            {
                Document = RavenJObject.FromObject(endpoint),
                Etag = null,
                Key = endpoint.Id.ToString(),
                Metadata = KnownEndpointMetadata
            };

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
                context.Extensions.Set(enricherContext.NewEndpoints);
            }
            catch (Exception e)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Processing of message '{context.MessageId}' failed.", e);
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

        readonly IEnrichImportedErrorMessages[] enrichers;
        readonly IDomainEvents domainEvents;
        readonly Counter ingestedCounter;
        BodyStorageEnricher bodyStorageEnricher;
        FailedMessageFactory failedMessageFactory;
        static readonly RavenJObject FailedMessageMetadata;
        static readonly RavenJObject KnownEndpointMetadata;
        static readonly JsonSerializer Serializer;
        static readonly ILog Logger = LogManager.GetLogger<ErrorProcessor>();
    }
}