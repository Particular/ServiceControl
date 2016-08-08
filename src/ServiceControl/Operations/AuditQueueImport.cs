﻿namespace ServiceControl.Operations
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

    public class AuditQueueImport : IAdvancedSatellite, IDisposable
    {
        public IBuilder Builder { get; set; }
        public ISendMessages Forwarder { get; set; }
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
            var receivedMessage = new ImportSuccessfullyProcessedMessage(message);

            using (var childBuilder = Builder.CreateChildBuilder())
            {
                PipelineExecutor.CurrentContext.Set(childBuilder);

                foreach (var enricher in childBuilder.BuildAll<IEnrichImportedMessages>())
                {
                    enricher.Enrich(receivedMessage);
                }

                var logicalMessage = LogicalMessageFactory.Create(receivedMessage);

                var context = new IncomingContext(PipelineExecutor.CurrentContext, message)
                {
                    LogicalMessages = new List<LogicalMessage>
                    {
                        logicalMessage
                    },
                    IncomingLogicalMessage = logicalMessage
                };

                context.Set("NServiceBus.CallbackInvocationBehavior.CallbackWasInvoked", false);

                var behaviors = behavioursToAddFirst.Concat(PipelineExecutor.Incoming.SkipWhile(r => r.StepId != WellKnownStep.LoadHandlers).Select(r => r.BehaviorType));

                PipelineExecutor.InvokePipeline(behaviors, context);
            }

            if (Settings.ForwardAuditMessages)
            {
                TransportMessageCleaner.CleanForForwarding(message);
                Forwarder.Send(message, new SendOptions(Settings.AuditLogQueue));
            }
        }

        Type[] behavioursToAddFirst = new[] { typeof(RavenUnitOfWorkBehavior) };

        public void Start()
        {
            if (!TerminateIfForwardingIsEnabledButQueueNotWritable())
            {
                Logger.InfoFormat("Audit import is now started, feeding audit messages from: {0}", InputAddress);
            }
        }

        bool TerminateIfForwardingIsEnabledButQueueNotWritable()
        {
            if (!Settings.ForwardAuditMessages)
            {
                return false;
            }

            try
            {
                //Send a message to test the forwarding queue
                var testMessage = new TransportMessage(Guid.Empty.ToString("N"), new Dictionary<string, string>());
                Forwarder.Send(testMessage, new SendOptions(Settings.AuditLogQueue));
                return false;
            }
            catch (Exception messageForwardingException)
            {
                CriticalError.Raise("Audit Import cannot start", messageForwardingException);
                return true;
            }
        }


        public void Stop()
        {
        }

        public Address InputAddress => Settings.AuditQueue;

        public bool Disabled => false;

        public Action<TransportReceiver> GetReceiverCustomization()
        {
            satelliteImportFailuresHandler = new SatelliteImportFailuresHandler(Builder.Build<IDocumentStore>(),
                Path.Combine(LoggingSettings.LogPath, @"FailedImports\Audit"), tm => new FailedAuditImport
                {
                    Message = tm,
                },
                CriticalError);

            return receiver => { receiver.FailureManager = satelliteImportFailuresHandler; };
        }

        public void Dispose()
        {
            satelliteImportFailuresHandler?.Dispose();
        }

        SatelliteImportFailuresHandler satelliteImportFailuresHandler;

        static readonly ILog Logger = LogManager.GetLogger(typeof(AuditQueueImport));
    }
}