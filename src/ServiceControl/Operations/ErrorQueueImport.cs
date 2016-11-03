namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Metrics;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NServiceBus.Unicast.Transport;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using Raven.Imports.Newtonsoft.Json;
    using Raven.Json.Linq;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Handlers;
    using JsonSerializer = Raven.Imports.Newtonsoft.Json.JsonSerializer;

    public class ErrorQueueImport : IAdvancedSatellite, IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ErrorQueueImport));
        private static readonly RavenJObject JObjectMetadata;
        private static readonly JsonSerializer Serializer;

        private readonly Timer timer = Metric.Timer("Error messages processed", Unit.Custom("Messages"));
        private readonly IBuilder builder;
        private readonly IBus bus;
        private readonly CriticalError criticalError;
        private readonly ISendMessages forwarder;
        private readonly LoggingSettings loggingSettings;
        private readonly Settings settings;
        private readonly IDocumentStore store;
        private readonly IEnrichImportedMessages[] enrichers;
        private readonly IFailedMessageEnricher[] failedEnrichers;
        private SatelliteImportFailuresHandler satelliteImportFailuresHandler;

        static ErrorQueueImport()
        {
            Serializer = JsonExtensions.CreateDefaultJsonSerializer();
            Serializer.TypeNameHandling = TypeNameHandling.Auto;

            JObjectMetadata = RavenJObject.Parse($@"
                                    {{
                                        ""Raven-Entity-Name"": ""{FailedMessage.CollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(FailedMessage).AssemblyQualifiedName}""
                                    }}");
        }

        public ErrorQueueImport(IBuilder builder, ISendMessages forwarder, IDocumentStore store, IBus bus, CriticalError criticalError, LoggingSettings loggingSettings, Settings settings)
        {
            this.builder = builder;
            this.forwarder = forwarder;
            this.store = store;
            this.bus = bus;
            this.criticalError = criticalError;
            this.loggingSettings = loggingSettings;
            this.settings = settings;

            enrichers = builder.BuildAll<IEnrichImportedMessages>().ToArray();
            failedEnrichers = builder.BuildAll<IFailedMessageEnricher>().ToArray();
        }

        public bool Handle(TransportMessage message)
        {
            using (timer.NewContext())
            {
                InnerHandle(message);
            }

            return true;
        }

        public void Start()
        {
            if (!TerminateIfForwardingQueueNotWritable())
            {
                Logger.InfoFormat("Error import is now started, feeding error messages from: {0}", InputAddress);
            }
        }

        public void Stop()
        {
        }

        public Address InputAddress => settings.ErrorQueue;

        public bool Disabled => InputAddress == Address.Undefined;

        public Action<TransportReceiver> GetReceiverCustomization()
        {
            satelliteImportFailuresHandler = new SatelliteImportFailuresHandler(builder.Build<IDocumentStore>(),
                Path.Combine(loggingSettings.LogPath, @"FailedImports\Error"), tm => new FailedErrorImport
                {
                    Message = tm
                }, criticalError);

            return receiver => { receiver.FailureManager = satelliteImportFailuresHandler; };
        }

        public void Dispose()
        {
            satelliteImportFailuresHandler?.Dispose();
        }

        private void InnerHandle(TransportMessage message)
        {
            var errorMessageReceived = new ImportFailedMessage(message);

            foreach (var enricher in enrichers)
            {
                enricher.Enrich(errorMessageReceived);
            }

            Handle(errorMessageReceived);

            if (settings.ForwardErrorMessages)
            {
                TransportMessageCleaner.CleanForForwarding(message);
                forwarder.Send(message, new SendOptions(settings.ErrorLogQueue));
            }
        }

        private void Handle(ImportFailedMessage message)
        {
            var uniqueId = message.UniqueMessageId;
            var documentId = $"{FailedMessage.CollectionName}/{uniqueId}";
            var timeOfFailure = message.FailureDetails.TimeOfFailure;
            var headers = message.PhysicalMessage.Headers;
            var metadata = message.Metadata;
            var intent = message.PhysicalMessage.MessageIntent;
            var groups = new List<FailedMessage.FailureGroup>();
            var recoverable = message.PhysicalMessage.Recoverable;
            var correlationId = message.PhysicalMessage.CorrelationId;
            var replyToAddress = message.PhysicalMessage.ReplyToAddress;
            var failureDetails = message.FailureDetails;

            foreach (var enricher in failedEnrichers)
            {
                groups.AddRange(enricher.Enrich((string) metadata["MessageType"], failureDetails));
            }

            store.DatabaseCommands.Patch(documentId, new[]
                {
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.Status),
                        Type = PatchCommandType.Set,
                        Value = (int) FailedMessageStatus.Unresolved
                    },
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.ProcessingAttempts),
                        Type = PatchCommandType.Add,
                        Value = RavenJToken.FromObject(new FailedMessage.ProcessingAttempt
                        {
                            AttemptedAt = timeOfFailure,
                            FailureDetails = failureDetails,
                            MessageMetadata = metadata,
                            MessageId = headers[Headers.MessageId],
                            Headers = headers,
                            ReplyToAddress = replyToAddress,
                            Recoverable = recoverable,
                            CorrelationId = correlationId,
                            MessageIntent = intent
                        }, Serializer) // Need to specify serializer here because otherwise the $type for EndpointDetails is missing and this causes EventDispatcher to blow up!
                    },
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.FailureGroups),
                        Type = PatchCommandType.Set,
                        Value = RavenJToken.FromObject(groups)
                    }
                },
                new[]
                {
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.UniqueMessageId),
                        Type = PatchCommandType.Set,
                        Value = uniqueId
                    },
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.Status),
                        Type = PatchCommandType.Set,
                        Value = (int) FailedMessageStatus.Unresolved
                    },
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.ProcessingAttempts),
                        Type = PatchCommandType.Add,
                        Value = RavenJToken.FromObject(new FailedMessage.ProcessingAttempt
                        {
                            AttemptedAt = timeOfFailure,
                            FailureDetails = failureDetails,
                            MessageMetadata = metadata,
                            MessageId = headers[Headers.MessageId],
                            Headers = headers,
                            ReplyToAddress = replyToAddress,
                            Recoverable = recoverable,
                            CorrelationId = correlationId,
                            MessageIntent = intent
                        }, Serializer) // Need to specify serilaizer here because otherwise the $type for EndpointDetails is missing and this causes EventDispatcher to blow up!
                    },
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.FailureGroups),
                        Type = PatchCommandType.Set,
                        Value = RavenJToken.FromObject(groups)
                    }
                }, JObjectMetadata);

            string failedMessageId;
            if (message.PhysicalMessage.Headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out failedMessageId))
            {
                bus.Publish<MessageFailedRepeatedly>(m =>
                {
                    m.FailureDetails = message.FailureDetails;
                    m.EndpointId = message.FailingEndpointId;
                    m.FailedMessageId = failedMessageId;
                });
            }
            else
            {
                bus.Publish<MessageFailed>(m =>
                {
                    m.FailureDetails = message.FailureDetails;
                    m.EndpointId = message.FailingEndpointId;
                    m.FailedMessageId = message.UniqueMessageId;
                });
            }
        }

        private bool TerminateIfForwardingQueueNotWritable()
        {
            if (!settings.ForwardErrorMessages)
            {
                return false;
            }

            try
            {
                //Send a message to test the forwarding queue
                var testMessage = new TransportMessage(Guid.Empty.ToString("N"), new Dictionary<string, string>());
                forwarder.Send(testMessage, new SendOptions(settings.ErrorLogQueue));
                return false;
            }
            catch (Exception messageForwardingException)
            {
                criticalError.Raise("Error Import cannot start", messageForwardingException);
                return true;
            }
        }
    }
}