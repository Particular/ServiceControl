namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NServiceBus.Unicast.Transport;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageAuditing;

    public class AuditQueueImport : IAdvancedSatellite, IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(AuditQueueImport));
        private readonly IBuilder builder;
        private readonly CriticalError criticalError;
        private readonly IEnrichImportedMessages[] enrichers;
        private readonly ISendMessages forwarder;
        private readonly LoggingSettings loggingSettings;
        private readonly Settings settings;
        private readonly IDocumentStore store;
        private SatelliteImportFailuresHandler satelliteImportFailuresHandler;

        public AuditQueueImport(IBuilder builder, ISendMessages forwarder, IDocumentStore store, CriticalError criticalError, LoggingSettings loggingSettings, Settings settings)
        {
            this.builder = builder;
            this.forwarder = forwarder;
            this.store = store;

            this.criticalError = criticalError;
            this.loggingSettings = loggingSettings;
            this.settings = settings;

            enrichers = builder.BuildAll<IEnrichImportedMessages>().ToArray();
        }

        public bool Handle(TransportMessage message)
        {
            InnerHandle(message);

            return true;
        }

        public void Start()
        {
            if (!TerminateIfForwardingIsEnabledButQueueNotWritable())
            {
                Logger.Info($"Audit import is now started, feeding audit messages from: {InputAddress}");
            }
        }

        public void Stop()
        {
        }

        public Address InputAddress => settings.AuditQueue;

        public bool Disabled => false;

        public Action<TransportReceiver> GetReceiverCustomization()
        {
            satelliteImportFailuresHandler = new SatelliteImportFailuresHandler(builder.Build<IDocumentStore>(),
                Path.Combine(loggingSettings.LogPath, @"FailedImports\Audit"), tm => new FailedAuditImport
                {
                    Message = tm
                },
                criticalError);

            return receiver => { receiver.FailureManager = satelliteImportFailuresHandler; };
        }

        public void Dispose()
        {
            satelliteImportFailuresHandler?.Dispose();
        }

        private void InnerHandle(TransportMessage message)
        {
            var entity = ConvertToSaveMessage(message);
            using (var session = store.OpenSession())
            {
                session.Store(entity);
                session.SaveChanges();
            }

            if (settings.ForwardAuditMessages)
            {
                TransportMessageCleaner.CleanForForwarding(message);
                forwarder.Send(message, new SendOptions(settings.AuditLogQueue));
            }
        }

        private ProcessedMessage ConvertToSaveMessage(TransportMessage message)
        {
            var receivedMessage = new ImportSuccessfullyProcessedMessage(message);

            foreach (var enricher in enrichers)
            {
                enricher.Enrich(receivedMessage);
            }

            var auditMessage = new ProcessedMessage(receivedMessage)
            {
                // We do this so Raven does not spend time assigning a hilo key
                Id = $"ProcessedMessages/{Guid.NewGuid()}"
            };
            return auditMessage;
        }

        private bool TerminateIfForwardingIsEnabledButQueueNotWritable()
        {
            if (!settings.ForwardAuditMessages)
            {
                return false;
            }

            try
            {
                //Send a message to test the forwarding queue
                var testMessage = new TransportMessage(Guid.Empty.ToString("N"), new Dictionary<string, string>());
                forwarder.Send(testMessage, new SendOptions(settings.AuditLogQueue));
                return false;
            }
            catch (Exception messageForwardingException)
            {
                criticalError.Raise("Audit Import cannot start", messageForwardingException);
                return true;
            }
        }
    }
}