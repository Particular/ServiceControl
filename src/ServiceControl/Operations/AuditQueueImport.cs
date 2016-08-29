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
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageAuditing.Handlers;
    using ServiceControl.Operations.BodyStorage;

    public class AuditQueueImport : IAdvancedSatellite, IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(AuditQueueImport));
        private readonly IBuilder builder;
        private readonly CriticalError criticalError;
        private readonly ISendMessages forwarder;
        private readonly LoggingSettings loggingSettings;

        private readonly Settings settings;
        private readonly StoreBody storeBody;
        private readonly Timer timer = Metric.Timer("Audit messages dequeued", Unit.Requests);
        private SatelliteImportFailuresHandler satelliteImportFailuresHandler;
        private AuditMessageHandler auditMessageHandler;
        private ProcessAudits processAudits;

        public AuditQueueImport(IBuilder builder, ISendMessages forwarder, IDocumentStore store, CriticalError criticalError, LoggingSettings loggingSettings, Settings settings, StoreBody storeBody)
        {
            this.builder = builder;
            this.forwarder = forwarder;

            this.criticalError = criticalError;
            this.loggingSettings = loggingSettings;
            this.settings = settings;
            this.storeBody = storeBody;

            auditMessageHandler = new AuditMessageHandler(store, builder.BuildAll<IEnrichImportedMessages>().ToArray());

            processAudits = new ProcessAudits(builder, store, storeBody);
        }

        public bool Handle(TransportMessage message)
        {
            using (timer.NewContext())
            {
                InnerHandle(message);

                if (settings.ForwardAuditMessages)
                {
                    TransportMessageCleaner.CleanForForwarding(message);
                    forwarder.Send(message, new SendOptions(settings.AuditLogQueue));
                }
            }

            return true;
        }

        public void Start()
        {
            if (!TerminateIfForwardingIsEnabledButQueueNotWritable())
            {
                Logger.Info($"Audit import is now started, feeding audit messages from: {InputAddress}");
            }

            processAudits.Start();
        }

        public void Stop()
        {
            processAudits.Stop();
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
            ImportSuccessfullyProcessedMessage receivedMessage;

            if (storeBody.SaveInStorage(message, out receivedMessage))
            {
                storeBody.SaveAuditToBeProcessedLater(message);
                return;
            }

            auditMessageHandler.Handle(receivedMessage);
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