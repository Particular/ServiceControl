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
    using ServiceControl.MessageFailures.Handlers;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Operations.Error;

    public class ErrorQueueImport : IAdvancedSatellite, IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(ErrorQueueImport));

        private readonly IBuilder builder;
        private readonly ISendMessages forwarder;
        private readonly CriticalError criticalError;
        private readonly LoggingSettings loggingSettings;
        private readonly Settings settings;
        private readonly IMessageBodyStore messageBodyStore;
        private SatelliteImportFailuresHandler satelliteImportFailuresHandler;
        private readonly Timer timer = Metric.Timer("Error messages dequeue", Unit.Custom("Messages"));
        private ProcessErrors processErrors;
        private MessageBodyFactory messageBodyFactory;
        private ErrorIngestionCache errorIngestionCache;
        private ErrorMessageBodyStoragePolicy errorMessageBodyStoragePolicy;

        public ErrorQueueImport(IBuilder builder, ISendMessages forwarder, IDocumentStore store, IBus bus, CriticalError criticalError, LoggingSettings loggingSettings, Settings settings, IMessageBodyStore messageBodyStore)
        {
            this.builder = builder;
            this.forwarder = forwarder;
            this.criticalError = criticalError;
            this.loggingSettings = loggingSettings;
            this.settings = settings;
            this.messageBodyStore = messageBodyStore;
            messageBodyFactory = new MessageBodyFactory();
            errorIngestionCache = new ErrorIngestionCache(settings);
            errorMessageBodyStoragePolicy = new ErrorMessageBodyStoragePolicy(settings);
            processErrors = new ProcessErrors(store, errorIngestionCache, new PatchCommandDataFactory(builder.BuildAll<IFailedMessageEnricher>().ToArray(), builder.BuildAll<IEnrichImportedMessages>().Where(x => x.EnrichAudits).ToArray(), errorMessageBodyStoragePolicy, messageBodyStore), bus);
        }

        public bool Handle(TransportMessage message)
        {
            using (timer.NewContext())
            {
                InnerHandle(message);

                if (settings.ForwardErrorMessages)
                {
                    TransportMessageCleaner.CleanForForwarding(message);
                    forwarder.Send(message, new SendOptions(settings.ErrorLogQueue));
                }
            }

            return true;
        }

        void InnerHandle(TransportMessage message)
        {
            var metadata = messageBodyFactory.Create(message);
            var claimCheck = messageBodyStore.Store(message.Body, metadata, errorMessageBodyStoragePolicy);

            errorIngestionCache.Write(message.Headers, message.Recoverable, claimCheck);
        }

        public void Start()
        {
            if (!TerminateIfForwardingQueueNotWritable())
            {
                Logger.Info($"Error import is now started, feeding error messages from: {InputAddress}");
            }

            processErrors.Start();
        }

        public void Stop()
        {
            processErrors.Stop();
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