﻿namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
    using ServiceControl.Operations.BodyStorage;

    public class ErrorQueueImport : IAdvancedSatellite, IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(ErrorQueueImport));

        private readonly IBuilder builder;
        private readonly ISendMessages forwarder;
        private readonly StoreBody storeBody;
        private readonly CriticalError criticalError;
        private readonly LoggingSettings loggingSettings;
        private readonly Settings settings;
        private SatelliteImportFailuresHandler satelliteImportFailuresHandler;
        private readonly Timer timer = Metric.Timer("Error messages", Unit.Items);

        public ErrorQueueImport(IBuilder builder, ISendMessages forwarder, StoreBody storeBody, CriticalError criticalError, LoggingSettings loggingSettings, Settings settings)
        {
            this.builder = builder;
            this.forwarder = forwarder;
            this.storeBody = storeBody;
            this.criticalError = criticalError;
            this.loggingSettings = loggingSettings;
            this.settings = settings;
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
            storeBody.SaveErrorToBeProcessedLater(message);
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