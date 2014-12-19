
namespace ServiceControl.Shell.Infrastructure.Ingestion
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Transport;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Shell.Api.Ingestion;

    [CompilerGenerated]
    class IngestorRunner<T> : IAdvancedSatellite, IDisposable
        where T : MessageIngestor
    {
        readonly T ingestor;
        readonly IBuilder builder;
        readonly ISendMessages forwarder;

        public IngestorRunner(T ingestor, IBuilder builder, ISendMessages forwarder)
        {
            this.ingestor = ingestor;
            this.builder = builder;
            this.forwarder = forwarder;
        }

        public bool Handle(TransportMessage message)
        {
            var ingestedMessage = new IngestedMessage(message.Headers, message.Body);
            ingestor.Process(ingestedMessage);

            if (Settings.ForwardAuditMessages)
            {
                forwarder.Send(message, Settings.AuditLogQueue);
            }

            return true;
        }

        public void Start()
        {
            Logger.InfoFormat("Ingestion from {0} is now started.", InputAddress);
        }

        public void Stop()
        {
        }

        public Address InputAddress
        {
            get { return Address.Parse(ingestor.Address); }
        }

        public bool Disabled
        {
            get { return false; }
        }

        public Action<TransportReceiver> GetReceiverCustomization()
        {
            satelliteImportFailuresHandler = new SatelliteImportFailuresHandler(builder.Build<IDocumentStore>(),
                Path.Combine(Settings.LogPath, @"FailedImports\Audit"), ingestor.Address);

            return receiver => { receiver.FailureManager = satelliteImportFailuresHandler; };
        }

        public void Dispose()
        {
            if (satelliteImportFailuresHandler != null)
            {
                satelliteImportFailuresHandler.Dispose();
            }
        }

        SatelliteImportFailuresHandler satelliteImportFailuresHandler;

        static readonly ILog Logger = LogManager.GetLogger(typeof(T));
       
    }
}
