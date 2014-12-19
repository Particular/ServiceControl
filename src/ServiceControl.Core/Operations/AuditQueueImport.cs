//namespace ServiceControl.Operations
//{
//    using System;
//    using System.IO;
//    using Contracts.Operations;
//    using NServiceBus;
//    using NServiceBus.Logging;
//    using NServiceBus.ObjectBuilder;
//    using NServiceBus.Pipeline;
//    using NServiceBus.Satellites;
//    using NServiceBus.Transports;
//    using NServiceBus.Unicast.Messages;
//    using NServiceBus.Unicast.Transport;
//    using Raven.Client;
//    using ServiceBus.Management.Infrastructure.Settings;

//    public class AuditQueueImport : IAdvancedSatellite, IDisposable
//    {
//        public IBuilder Builder { get; set; }
//        public ISendMessages Forwarder { get; set; }

//#pragma warning disable 618
//        public PipelineExecutor PipelineExecutor { get; set; }
//        public LogicalMessageFactory LogicalMessageFactory { get; set; }

//#pragma warning restore 618

//        public bool Handle(TransportMessage message)
//        {
//            InnerHandle(message);

//            return true;
//        }

//        void InnerHandle(TransportMessage message)
//        {
//            var receivedMessage = new ImportSuccessfullyProcessedMessage(message, message.UniqueId());

//            using (var childBuilder = Builder.CreateChildBuilder())
//            {
//                PipelineExecutor.CurrentContext.Set(childBuilder);

//                foreach (var enricher in childBuilder.BuildAll<IEnrichImportedMessages>())
//                {
//                    enricher.Enrich(receivedMessage);
//                }

//                var logicalMessage = LogicalMessageFactory.Create(typeof(ImportSuccessfullyProcessedMessage),
//                    receivedMessage);

//                PipelineExecutor.InvokeLogicalMessagePipeline(logicalMessage);
//            }

//            if (Settings.ForwardAuditMessages)
//            {
//                Forwarder.Send(message, Settings.AuditLogQueue);
//            }
//        }

//        public void Start()
//        {
//            Logger.InfoFormat("Audit import is now started, feeding audit messages from: {0}", InputAddress);
//        }

//        public void Stop()
//        {
//        }

//        public Address InputAddress
//        {
//            get { return Settings.AuditQueue; }
//        }

//        public bool Disabled
//        {
//            get { return false; }
//        }

//        public Action<TransportReceiver> GetReceiverCustomization()
//        {
//            satelliteImportFailuresHandler = new SatelliteImportFailuresHandler(Builder.Build<IDocumentStore>(),
//                Path.Combine(Settings.LogPath, @"FailedImports\Audit"), tm => new FailedAuditImport
//                {
//                    Message = tm,
//                });

//            return receiver => { receiver.FailureManager = satelliteImportFailuresHandler; };
//        }

//        public void Dispose()
//        {
//            if (satelliteImportFailuresHandler != null)
//            {
//                satelliteImportFailuresHandler.Dispose();
//            }
//        }

//        SatelliteImportFailuresHandler satelliteImportFailuresHandler;

//        static readonly ILog Logger = LogManager.GetLogger(typeof(AuditQueueImport));
//    }
//}