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
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Recoverability;
    using FailedMessage = ServiceControl.MessageFailures.FailedMessage;
    using JsonSerializer = Raven.Imports.Newtonsoft.Json.JsonSerializer;

    public class ErrorQueueImport : IAdvancedSatellite, IDisposable
    {
        static ILog Logger = LogManager.GetLogger(typeof(ErrorQueueImport));
        static RavenJObject JObjectMetadata;
        static JsonSerializer Serializer;

        Timer timer = Metric.Timer("Error messages processed", Unit.Custom("Messages"));
        IBuilder builder;
        IDomainEvents domainEvents;
        CriticalError criticalError;
        ISendMessages forwarder;
        LoggingSettings loggingSettings;
        Settings settings;
        IDocumentStore store;
        IEnrichImportedMessages[] enrichers;
        BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;
        FailedMessageFactory failedMessageFactory;
        SatelliteImportFailuresHandler satelliteImportFailuresHandler;

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

        public ErrorQueueImport(IBuilder builder, ISendMessages forwarder, IDocumentStore store, IDomainEvents domainEvents, CriticalError criticalError, LoggingSettings loggingSettings, Settings settings, BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher)
        {
            this.builder = builder;
            this.forwarder = forwarder;
            this.store = store;
            this.criticalError = criticalError;
            this.loggingSettings = loggingSettings;
            this.settings = settings;
            this.bodyStorageEnricher = bodyStorageEnricher;
            this.domainEvents = domainEvents;

            enrichers = builder.BuildAll<IEnrichImportedMessages>().Where(e => e.EnrichErrors).ToArray();
            var failedEnrichers = builder.BuildAll<IFailedMessageEnricher>().ToArray();

            failedMessageFactory = new FailedMessageFactory(failedEnrichers);
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

        public bool Disabled => InputAddress == Address.Undefined || InputAddress == null || !settings.IngestErrorMessages;

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
            var metadata = new Dictionary<string, object>
            {
                ["MessageId"] = message.Id,
                ["MessageIntent"] = message.MessageIntent,
                ["HeadersForSearching"] = string.Join(" ", message.Headers.Values)
            };

            foreach (var enricher in enrichers)
            {
                enricher.Enrich(message.Headers, metadata);
            }

            bodyStorageEnricher.StoreErrorMessageBody(
                message.Body,
                message.Headers,
                metadata);

            var failureDetails = failedMessageFactory.ParseFailureDetails(message.Headers);

            var replyToAddress = message.ReplyToAddress == null ? null : message.ReplyToAddress.ToString();

            var processingAttempt = failedMessageFactory.CreateProcessingAttempt(
                message.Headers,
                metadata,
                failureDetails,
                message.MessageIntent,
                message.Recoverable,
                message.CorrelationId,
                replyToAddress);

            var groups = failedMessageFactory.GetGroups((string) metadata["MessageType"], failureDetails, processingAttempt);

            Store(message.Headers.UniqueId(), processingAttempt, groups);

            AnnounceFailedMessage(message.Headers, failureDetails);

            if (settings.ForwardErrorMessages)
            {
                TransportMessageCleaner.CleanForForwarding(message);
                forwarder.Send(message, new SendOptions(settings.ErrorLogQueue));
            }
        }

        private void Store(string uniqueMessageId, FailedMessage.ProcessingAttempt processingAttempt, List<FailedMessage.FailureGroup> groups)
        {
            var documentId = FailedMessage.MakeDocumentId(uniqueMessageId);

            store.DatabaseCommands.Patch(documentId,
                new[]
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
                        Value = RavenJToken.FromObject(processingAttempt, Serializer) // Need to specify serializer here because otherwise the $type for EndpointDetails is missing and this causes EventDispatcher to blow up!
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
                        Value = uniqueMessageId
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
                        Value = RavenJToken.FromObject(processingAttempt, Serializer) // Need to specify serilaizer here because otherwise the $type for EndpointDetails is missing and this causes EventDispatcher to blow up!
                    },
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.FailureGroups),
                        Type = PatchCommandType.Set,
                        Value = RavenJToken.FromObject(groups)
                    }
                }, JObjectMetadata
            );
        }

        private void AnnounceFailedMessage(IReadOnlyDictionary<string, string> headers, FailureDetails failureDetails)
        {
            var failingEndpointId = Address.Parse(failureDetails.AddressOfFailingEndpoint).Queue;

            string failedMessageId;
            if (headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out failedMessageId))
            {
                domainEvents.Raise(new MessageFailed
                {
                    FailureDetails = failureDetails,
                    EndpointId = failingEndpointId,
                    FailedMessageId = failedMessageId,
                    RepeatedFailure = true
                });
            }
            else
            {
                domainEvents.Raise(new MessageFailed
                {
                    FailureDetails = failureDetails,
                    EndpointId = failingEndpointId,
                    FailedMessageId = headers.UniqueId(),
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