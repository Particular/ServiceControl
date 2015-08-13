namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using Contracts.Operations;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NServiceBus.Unicast.Messages;
    using NServiceBus.Unicast.Transport;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    public class ErrorQueueImport : IAdvancedSatellite, IDisposable
    {
        public ISendMessages Forwarder { get; set; }
        public IBuilder Builder { get; set; }
        public PipelineExecutor PipelineExecutor { get; set; }
        public LogicalMessageFactory LogicalMessageFactory { get; set; }
        public CriticalError CriticalError { get; set; }

        public bool Handle(TransportMessage message)
        {
            InnerHandle(message);

            return true;
        }

        void InnerHandle(TransportMessage message)
        {
            var errorMessageReceived = new ImportFailedMessage(message);

            var logicalMessage = LogicalMessageFactory.Create(errorMessageReceived);

            using (var childBuilder = Builder.CreateChildBuilder())
            {
                PipelineExecutor.CurrentContext.Set(childBuilder);

                foreach (var enricher in childBuilder.BuildAll<IEnrichImportedMessages>())
                {
                    enricher.Enrich(errorMessageReceived);
                }

                var context = new IncomingContext(PipelineExecutor.CurrentContext, message)
                {
                    LogicalMessages = new List<LogicalMessage>
                    {
                        logicalMessage
                    }
                };

                PipelineExecutor.InvokePipeline(PipelineExecutor.Incoming.Select(r => r.BehaviorType), context);
            }

            Forwarder.Send(message, new SendOptions(Settings.ErrorLogQueue));
        }

        public void Start()
        {

            if (TerminateIfForwardingQueueNotWritable())
            {
                return;
            }
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
            satelliteImportFailuresHandler = new SatelliteImportFailuresHandler(Builder.Build<IDocumentStore>(),
                Path.Combine(Settings.LogPath, @"FailedImports\Error"), tm => new FailedErrorImport
                {
                    Message = tm,
                }, CriticalError);

            return receiver => { receiver.FailureManager = satelliteImportFailuresHandler; };
        }

        bool TerminateIfForwardingQueueNotWritable()
        {
            try
            {
                //Send a message to test the forwarding queue
                var testMessage = new TransportMessage(Guid.Empty.ToString("N"), new Dictionary<string, string>());
                Forwarder.Send(testMessage, new SendOptions(Settings.ErrorLogQueue));
                return false;
            }
            catch (Exception messageForwardingException)
            {
                //This call to RaiseCriticalError has to be on a seperate thread  otherwise it deadlocks and doesn't stop correctly.  
                ThreadPool.QueueUserWorkItem(state => CriticalError.Raise("Error Import cannot start", messageForwardingException));
                return true;
            }
        }


        public void Dispose()
        {
            if (satelliteImportFailuresHandler != null)
            {
                satelliteImportFailuresHandler.Dispose();
            }
        }

        SatelliteImportFailuresHandler satelliteImportFailuresHandler;

        static readonly ILog Logger = LogManager.GetLogger(typeof(ErrorQueueImport));
    }
}