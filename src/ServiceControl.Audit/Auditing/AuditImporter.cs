namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Transport;
    using Raven.Client;

    class AuditImporter : Feature
    {
        public AuditImporter()
        {
            EnableByDefault();
            Prerequisite(c =>
            {
                var settings = c.Settings.Get<Settings>("ServiceControl.Settings");
                return settings.IngestAuditMessages;
            }, "Ingestion of audit messages has been disabled.");
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var settings = context.Settings.Get<Settings>("ServiceControl.Settings");

            context.Container.ConfigureComponent<AuditPersister>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<AuditIngestor>(DependencyLifecycle.SingleInstance);

            batcher = new MultiProducerConcurrentCompletion<ProcessAuditMessageContext>(settings.MaximumConcurrencyLevel, TimeSpan.FromMilliseconds(settings.MaximumConcurrencyLevel * 200), 5, 1);

            context.AddSatelliteReceiver(
                "Audit Import",
                context.Settings.ToTransportAddress(settings.AuditQueue),
                new PushRuntimeSettings(settings.MaximumConcurrencyLevel),
                OnAuditError,
                (builder, messageContext) => settings.OnMessage(messageContext.MessageId, messageContext.Headers, messageContext.Body, () => OnAuditMessage(new ProcessAuditMessageContext(messageContext, builder.Build<IMessageSession>())))
            );

            context.RegisterStartupTask(b => new StartupTask(CreateFailureHandler(b), b.Build<AuditIngestor>(), this));

            if (settings.ForwardAuditMessages)
            {
                context.RegisterStartupTask(b => new EnsureCanWriteToForwardingAddress(b.Build<IForwardMessages>(), settings.AuditLogQueue));
            }
        }

        static SatelliteImportFailuresHandler CreateFailureHandler(IBuilder b)
        {
            var documentStore = b.Build<IDocumentStore>();
            var logPath = Path.Combine(b.Build<LoggingSettings>().LogPath, @"FailedImports\Audit");

            return new SatelliteImportFailuresHandler(documentStore, logPath, msg => new FailedAuditImport
            {
                Message = msg
            }, b.Build<CriticalError>());
        }

        Task OnAuditMessage(ProcessAuditMessageContext context)
        {
            //var slotNumber = (int)Interlocked.Increment(ref increment) % 2;
            var slotNumber = 0;
            batcher.Push(context, slotNumber);
            return context.Completed.Task;
        }

        //static long increment;

        RecoverabilityAction OnAuditError(RecoverabilityConfig config, ErrorContext errorContext)
        {
            var recoverabilityAction = DefaultRecoverabilityPolicy.Invoke(config, errorContext);

            if (recoverabilityAction is MoveToError)
            {
                importFailuresHandler.Handle(errorContext).GetAwaiter().GetResult();
            }

            return recoverabilityAction;
        }

        SatelliteImportFailuresHandler importFailuresHandler;
        AuditIngestor auditIngestor;
        MultiProducerConcurrentCompletion<ProcessAuditMessageContext> batcher;

        class StartupTask : FeatureStartupTask
        {
            readonly AuditImporter auditImporter;

            public StartupTask(SatelliteImportFailuresHandler importFailuresHandler, AuditIngestor auditIngestor, AuditImporter importer)
            {
                auditImporter = importer;
                importer.importFailuresHandler = importFailuresHandler;
                importer.auditIngestor = auditIngestor;
            }

            protected override Task OnStart(IMessageSession session)
            {
                auditImporter.batcher.Start(Process);
                return Task.FromResult(0);
            }

            Task Process(List<ProcessAuditMessageContext> arg1, int arg2, object arg3, CancellationToken arg4)
            {
                return auditImporter.auditIngestor.Ingest(arg1);
            }

            protected override async Task OnStop(IMessageSession session)
            {
                await auditImporter.batcher.Complete(true).ConfigureAwait(false);
            }
        }

        class EnsureCanWriteToForwardingAddress : FeatureStartupTask
        {
            public EnsureCanWriteToForwardingAddress(IForwardMessages messageForwarder, string forwardingAddress)
            {
                this.messageForwarder = messageForwarder;
                this.forwardingAddress = forwardingAddress;
            }

            protected override Task OnStart(IMessageSession session)
            {
                return messageForwarder.VerifyCanReachForwardingAddress(forwardingAddress);
            }

            protected override Task OnStop(IMessageSession session)
            {
                return Task.CompletedTask;
            }

            readonly IForwardMessages messageForwarder;
            readonly string forwardingAddress;
        }
    }
}