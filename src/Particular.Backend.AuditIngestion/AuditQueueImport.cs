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

    public class AuditQueueImport : IAdvancedSatellite, IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(AuditQueueImport));

        readonly IBuilder builder;
        readonly ISendMessages forwarder;
        readonly TransportMessageProcessor transportMessageProcessor;
        SatelliteImportFailuresHandler satelliteImportFailuresHandler;
        readonly Meter throughputMetric;


        public AuditQueueImport(IBuilder builder, ISendMessages forwarder, TransportMessageProcessor transportMessageProcessor)
        {
            throughputMetric = Metric.Meter("Audit queue", "audits");
            this.builder = builder;
            this.forwarder = forwarder;
            this.transportMessageProcessor = transportMessageProcessor;
        }

        public bool Handle(TransportMessage message)
        {
            transportMessageProcessor.ProcessSuccessful(message);
            if (Settings.ForwardAuditMessages == true)
            {
                forwarder.Send(message, Settings.AuditLogQueue);
            }
            throughputMetric.Mark();
            return true;
        }

        public void Start()
        {
            Logger.InfoFormat("Audit import is now started, feeding audit messages from: {0}", InputAddress);
        }

        public void Stop()
        {
        }

        public Address InputAddress
        {
            get { return Settings.AuditQueue; }
        }

        public bool Disabled
        {
            get { return false; }
        }

        public Action<TransportReceiver> GetReceiverCustomization()
        {
            satelliteImportFailuresHandler = new SatelliteImportFailuresHandler(builder.Build<IDocumentStore>(),
                Path.Combine(Settings.LogPath, @"FailedImports\Audit"), tm => new FailedAuditImport
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