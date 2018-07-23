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
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var settings = context.Settings.Get<Settings>("ServiceControl.Settings");

            if (settings.IngestErrorMessages)
            {
                context.Container.ConfigureComponent<ErrorIngestor>(DependencyLifecycle.SingleInstance);
                context.Container.ConfigureComponent<FailedMessagePersister>(DependencyLifecycle.SingleInstance);
                context.Container.ConfigureComponent<FailedMessageAnnouncer>(DependencyLifecycle.SingleInstance);
                context.Container.ConfigureComponent(b =>
                    new SatelliteImportFailuresHandler(b.Build<IDocumentStore>(), Path.Combine(b.Build<LoggingSettings>().LogPath, @"FailedImports\Error"), msg => new FailedErrorImport
                    {
                        Message = msg
                    }, b.Build<CriticalError>()), DependencyLifecycle.SingleInstance);

                context.AddSatelliteReceiver(
                    "Error Queue Ingestor",
                    context.Settings.ToTransportAddress(settings.ErrorQueue),
                    new PushRuntimeSettings(settings.MaximumConcurrencyLevel),
                    OnError,
                    (builder, messageContext) => settings.OnMessage(messageContext.MessageId, messageContext.Headers, messageContext.Body, () => OnMessage(builder, messageContext))
                );
                
                context.RegisterStartupTask(b => new StartupTask(b.Build<SatelliteImportFailuresHandler>(), this));
            }

            // TODO: Fail startup if can't write to audit forwarding queue but forwarding is enabled
        }

        Task OnMessage(IBuilder builder, MessageContext messageContext)
        {
            return builder.Build<ErrorIngestor>().Ingest(messageContext);
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

        class StartupTask : FeatureStartupTask
        {
            public StartupTask(SatelliteImportFailuresHandler importFailuresHandler, ErrorImporter importer)
            {
                importer.importFailuresHandler = importFailuresHandler;
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
    }
}