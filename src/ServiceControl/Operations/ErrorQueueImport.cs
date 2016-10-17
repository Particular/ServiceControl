namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Contracts.Operations;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NServiceBus.Unicast.Transport;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Handlers;

    public class ErrorQueueImport : IAdvancedSatellite, IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(ErrorQueueImport));

        private readonly IBuilder builder;
        private readonly ISendMessages forwarder;
        private readonly IDocumentStore store;
        private readonly IBus bus;
        private readonly CriticalError criticalError;
        private readonly LoggingSettings loggingSettings;
        private readonly Settings settings;
        private SatelliteImportFailuresHandler satelliteImportFailuresHandler;
        private IEnrichImportedMessages[] enrichers;
        private IFailedMessageEnricher[] failedEnrichers;

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
            InnerHandle(message);

            return true;
        }

        void InnerHandle(TransportMessage message)
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

        void Handle(ImportFailedMessage message)
        {
            var documentId = FailedMessage.MakeDocumentId(message.UniqueMessageId);

            using (var session = store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var failure = session.Load<FailedMessage>(documentId) ?? new FailedMessage
                {
                    Id = documentId,
                    UniqueMessageId = message.UniqueMessageId
                };

                failure.Status = FailedMessageStatus.Unresolved;

                var timeOfFailure = message.FailureDetails.TimeOfFailure;

                //check for duplicate
                if (failure.ProcessingAttempts.Any(a => a.AttemptedAt == timeOfFailure))
                {
                    return;
                }

                failure.ProcessingAttempts.Add(new FailedMessage.ProcessingAttempt
                {
                    AttemptedAt = timeOfFailure,
                    FailureDetails = message.FailureDetails,
                    MessageMetadata = message.Metadata,
                    MessageId = message.PhysicalMessage.MessageId,
                    Headers = message.PhysicalMessage.Headers,
                    ReplyToAddress = message.PhysicalMessage.ReplyToAddress,
                    Recoverable = message.PhysicalMessage.Recoverable,
                    CorrelationId = message.PhysicalMessage.CorrelationId,
                    MessageIntent = message.PhysicalMessage.MessageIntent,
                });

                foreach (var enricher in failedEnrichers)
                {
                    enricher.Enrich(failure, message);
                }

                session.Store(failure);
                session.SaveChanges();

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
                    Message = tm,
                }, criticalError);

            return receiver => { receiver.FailureManager = satelliteImportFailuresHandler; };
        }

        bool TerminateIfForwardingQueueNotWritable()
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

        public void Dispose()
        {
            satelliteImportFailuresHandler?.Dispose();
        }
    }
}