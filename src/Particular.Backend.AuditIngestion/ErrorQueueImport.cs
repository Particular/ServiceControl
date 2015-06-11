namespace ServiceControl.Operations
{
    using System;
    using System.IO;
    using Metrics;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Transport;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.MessageTypes;

    public class ErrorQueueImport : IAdvancedSatellite, IDisposable
    {
        readonly ISendMessages forwarder;
        readonly IBuilder builder;
        readonly TransportMessageProcessor transportMessageProcessor;
        SatelliteImportFailuresHandler satelliteImportFailuresHandler;

        static readonly ILog Logger = LogManager.GetLogger(typeof(ErrorQueueImport));
        readonly Meter throughputMetric;

        public ErrorQueueImport(ISendMessages forwarder, IBuilder builder, TransportMessageProcessor transportMessageProcessor)
        {
            this.forwarder = forwarder;
            this.builder = builder;
            this.transportMessageProcessor = transportMessageProcessor;
            throughputMetric = Metric.Meter("Error queue", "errors");
        }

        public bool Handle(TransportMessage message)
        {
            transportMessageProcessor.ProcessFailed(message);
            forwarder.Send(message, Settings.ErrorLogQueue);
            throughputMetric.Mark();
            return true;
        }

        public void Start()
        {
            Logger.InfoFormat("Error import is now started, feeding error messages from: {0}", InputAddress);
        }

        public void Stop()
        {
        }

        public Address InputAddress
        {
            get { return Settings.ErrorQueue; }
        }

        public bool Disabled
        {
            get { return InputAddress == Address.Undefined; }
        }

        public Action<TransportReceiver> GetReceiverCustomization()
        {
            satelliteImportFailuresHandler = new SatelliteImportFailuresHandler(builder.Build<IDocumentStore>(),
                Path.Combine(Settings.LogPath, @"FailedImports\Error"), tm => new FailedErrorImport
                {
                    Message = tm,
                });

            return receiver => { receiver.FailureManager = satelliteImportFailuresHandler; };
        }

        public void Dispose()
        {
            if (satelliteImportFailuresHandler != null)
            {
                satelliteImportFailuresHandler.Dispose();
            }
        }

        
    }
}