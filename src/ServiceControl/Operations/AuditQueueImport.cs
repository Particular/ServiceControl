namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using Contracts.Operations;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Pipeline;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using NServiceBus.Transports.Msmq;
    using NServiceBus.Unicast.Messages;
    using NServiceBus.Unicast.Transport;
    using ServiceBus.Management.Infrastructure.Settings;

    public class AuditQueueImport : IAdvancedSatellite, IDisposable
    {
        public IBuilder Builder { get; set; }
        public ISendMessages Forwarder { get; set; }

#pragma warning disable 618
        public PipelineExecutor PipelineExecutor { get; set; }
        public LogicalMessageFactory LogicalMessageFactory { get; set; }

#pragma warning restore 618

        public AuditQueueImport(IDequeueMessages receiver)
        {
            disabled = receiver is MsmqDequeueStrategy;
        }

        public bool Handle(TransportMessage message)
        {
            InnerHandle(message);

            return true;
        }

        void InnerHandle(TransportMessage message)
        {
            var receivedMessage = new ImportSuccessfullyProcessedMessage(message);

            using (var childBuilder = Builder.CreateChildBuilder())
            {
                PipelineExecutor.CurrentContext.Set(childBuilder);

                foreach (var enricher in childBuilder.BuildAll<IEnrichImportedMessages>())
                {
                    enricher.Enrich(receivedMessage);
                }

                var logicalMessage = LogicalMessageFactory.Create(typeof(ImportSuccessfullyProcessedMessage),
                    receivedMessage);

                PipelineExecutor.InvokeLogicalMessagePipeline(logicalMessage);
            }

            if (Settings.ForwardAuditMessages == true)
            {
                Forwarder.Send(message, Settings.AuditLogQueue);
            }
        }

        public void Start()
        {
            if (!TerminateIfForwardingIsEnabledButQueueNotWritable())
            {
                Logger.InfoFormat("Audit import is now started, feeding audit messages from: {0}", InputAddress);    
            }
        }

        bool TerminateIfForwardingIsEnabledButQueueNotWritable()
        {
            if (Settings.ForwardAuditMessages != true)
            {
                return false;
            }

            try
            {
                //Send a message to test the forwarding queue
                var testMessage = new TransportMessage(Guid.Empty.ToString("N"), new Dictionary<string, string>());
                Forwarder.Send(testMessage, Settings.AuditLogQueue);
                return false;
            }
            catch (Exception messageForwardingException)
            {
                //This call to RaiseCriticalError has to be on a seperate thread  otherwise it deadlocks and doesn't stop correctly.  
                ThreadPool.QueueUserWorkItem(state => Configure.Instance.RaiseCriticalError(string.Format("Audit Import cannot start"), messageForwardingException));
                return true;
            }
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
            get { return disabled; }
        }

        public Action<TransportReceiver> GetReceiverCustomization()
        {
            satelliteImportFailuresHandler = new SatelliteImportFailuresHandler(Forwarder, Settings.AuditImportFailureQueue, Path.Combine(Settings.LogPath, @"FailedImports\Audit"));

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

        static readonly ILog Logger = LogManager.GetLogger(typeof(AuditQueueImport));
        bool disabled;
    }
}