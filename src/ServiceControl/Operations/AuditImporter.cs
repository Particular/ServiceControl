namespace ServiceControl.Operations
{
    using System.IO;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Transport;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

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

            context.AddSatelliteReceiver(
                "Audit Import",
                context.Settings.ToTransportAddress(settings.AuditQueue),
                new PushRuntimeSettings(settings.MaximumConcurrencyLevel),
                OnAuditError,
                (builder, messageContext) => settings.OnMessage(messageContext.MessageId, messageContext.Headers, messageContext.Body, () => OnAuditMessage(messageContext))
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

        Task OnAuditMessage(MessageContext messageContext)
        {
            return auditIngestor.Ingest(messageContext);
        }

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

        class StartupTask : FeatureStartupTask
        {
            public StartupTask(SatelliteImportFailuresHandler importFailuresHandler, AuditIngestor auditIngestor, AuditImporter importer)
            {
                importer.importFailuresHandler = importFailuresHandler;
                importer.auditIngestor = auditIngestor;
            }

            protected override Task OnStart(IMessageSession session)
            {
                return Task.FromResult(0);
            }

            protected override Task OnStop(IMessageSession session)
            {
                return Task.FromResult(0);
            }
        }

        class EnsureCanWriteToForwardingAddress : FeatureStartupTask
        {
            readonly IForwardMessages messageForwarder;
            readonly string forwardingAddress;

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
        }
    }
}