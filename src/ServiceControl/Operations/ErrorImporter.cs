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

    class ErrorImporter : Feature
    {
        public ErrorImporter()
        {
            EnableByDefault();
            Prerequisite(c =>
            {
                var settings = c.Settings.Get<Settings>("ServiceControl.Settings");
                return settings.IngestErrorMessages;
            }, "Ingestion of failed messages has been disabled.");
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var settings = context.Settings.Get<Settings>("ServiceControl.Settings");

            context.Container.ConfigureComponent<ErrorIngestor>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<ErrorPersister>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<FailedMessageAnnouncer>(DependencyLifecycle.SingleInstance);

            context.AddSatelliteReceiver(
                "Error Queue Ingestor",
                context.Settings.ToTransportAddress(settings.ErrorQueue),
                new PushRuntimeSettings(settings.MaximumConcurrencyLevel),
                OnError,
                (builder, messageContext) => settings.OnMessage(messageContext.MessageId, messageContext.Headers, messageContext.Body, () => OnMessage(messageContext))
            );

            context.RegisterStartupTask(b => new StartupTask(CreateFailureHandler(b), b.Build<ErrorIngestor>(), this));

            if (settings.ForwardErrorMessages)
            {
                context.RegisterStartupTask(b => new EnsureCanWriteToForwardingAddress(b.Build<IForwardMessages>(), settings.ErrorLogQueue));
            }
        }

        static SatelliteImportFailuresHandler CreateFailureHandler(IBuilder b)
        {
            var documentStore = b.Build<IDocumentStore>();
            var logPath = Path.Combine(b.Build<LoggingSettings>().LogPath, @"FailedImports\Error");
            return new SatelliteImportFailuresHandler(documentStore, logPath, msg => new FailedErrorImport
            {
                Message = msg
            }, b.Build<CriticalError>());
        }

        Task OnMessage(MessageContext messageContext)
        {
            return errorIngestor.Ingest(messageContext);
        }

        RecoverabilityAction OnError(RecoverabilityConfig config, ErrorContext errorContext)
        {
            var recoverabilityAction = DefaultRecoverabilityPolicy.Invoke(config, errorContext);

            if (recoverabilityAction is MoveToError)
            {
                importFailuresHandler.Handle(errorContext).GetAwaiter().GetResult();
            }

            return recoverabilityAction;
        }

        SatelliteImportFailuresHandler importFailuresHandler;
        ErrorIngestor errorIngestor;

        class StartupTask : FeatureStartupTask
        {
            public StartupTask(SatelliteImportFailuresHandler importFailuresHandler, ErrorIngestor errorIngestor, ErrorImporter importer)
            {
                importer.importFailuresHandler = importFailuresHandler;
                importer.errorIngestor = errorIngestor;
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