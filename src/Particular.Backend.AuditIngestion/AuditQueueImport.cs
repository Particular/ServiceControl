namespace ServiceControl.Operations
{
    using System;
    using System.IO;
    using System.Threading;
    using Contracts.Operations;
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


        public AuditQueueImport(IBuilder builder, ISendMessages forwarder, TransportMessageProcessor transportMessageProcessor)
        {
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
            return true;
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