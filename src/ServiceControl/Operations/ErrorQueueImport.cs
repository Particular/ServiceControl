namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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
    using ServiceControl.Infrastructure.RavenDB;

    public class ErrorQueueImport : IAdvancedSatellite, IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(ErrorQueueImport));

        private readonly IBuilder builder;
        private readonly ISendMessages forwarder;
        private readonly PipelineExecutor pipelineExecutor;
        private readonly LogicalMessageFactory logicalMessageFactory;
        private readonly CriticalError criticalError;
        private readonly LoggingSettings loggingSettings;
        private readonly Settings settings;
        private IEnumerable<Type> behaviors;
        private SatelliteImportFailuresHandler satelliteImportFailuresHandler;
        private IEnumerable<IEnrichImportedMessages> enrichers;

        public ErrorQueueImport(IBuilder builder, ISendMessages forwarder, PipelineExecutor pipelineExecutor, LogicalMessageFactory logicalMessageFactory, CriticalError criticalError, LoggingSettings loggingSettings, Settings settings)
        {
            this.builder = builder;
            this.forwarder = forwarder;
            this.pipelineExecutor = pipelineExecutor;
            this.logicalMessageFactory = logicalMessageFactory;
            this.criticalError = criticalError;
            this.loggingSettings = loggingSettings;
            this.settings = settings;

            behaviors = behavioursToAddFirst.Concat(pipelineExecutor.Incoming.SkipWhile(r => r.StepId != WellKnownStep.LoadHandlers).Select(r => r.BehaviorType));
            enrichers = builder.BuildAll<IEnrichImportedMessages>();
        }

        public bool Handle(TransportMessage message)
        {
            InnerHandle(message);

            return true;
        }

        void InnerHandle(TransportMessage message)
        {
            var errorMessageReceived = new ImportFailedMessage(message);

            using (var childBuilder = builder.CreateChildBuilder())
            {
                pipelineExecutor.CurrentContext.Set(childBuilder);

                foreach (var enricher in enrichers)
                {
                    enricher.Enrich(errorMessageReceived);
                }

                var logicalMessage = logicalMessageFactory.Create(errorMessageReceived);

                var context = new IncomingContext(pipelineExecutor.CurrentContext, message)
                {
                    IncomingLogicalMessage = logicalMessage
                };
                context.LogicalMessages.Add(logicalMessage);

                context.Set("NServiceBus.CallbackInvocationBehavior.CallbackWasInvoked", false);
               
                pipelineExecutor.InvokePipeline(behaviors, context);
            }
            if (settings.ForwardErrorMessages)
            {
                TransportMessageCleaner.CleanForForwarding(message);
                forwarder.Send(message, new SendOptions(settings.ErrorLogQueue));
            }
        }

        Type[] behavioursToAddFirst = new[] { typeof(RavenUnitOfWorkBehavior) };

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