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
        private SatelliteImportFailuresHandler importFailuresHandler;

        public ErrorImporter()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var settings = context.Settings.Get<Settings>();

            if (settings.IngestErrorMessages)
            {
                context.Container.ConfigureComponent<ErrorIngestor>(DependencyLifecycle.SingleInstance);
                context.Container.ConfigureComponent<FailedMessagePersister>(DependencyLifecycle.SingleInstance);
                context.Container.ConfigureComponent<FailedMessageAnnouncer>(DependencyLifecycle.SingleInstance);

                SetupImportFailuresHandler(context);

                context.AddSatelliteReceiver(
                    "Error Queue Ingestor", 
                    context.Settings.ToTransportAddress(settings.ErrorQueue), 
                    new PushRuntimeSettings(settings.MaximumConcurrencyLevel),
                    OnError,
                    (builder, messageContext) => settings.OnMessage(messageContext.MessageId, messageContext.Headers, messageContext.Body, () => OnMessage(builder, messageContext))
                    );
            }

            // TODO: Fail startup if can't write to audit forwarding queue but forwarding is enabled
        }

        private void SetupImportFailuresHandler(FeatureConfigurationContext context)
        {
            var store = context.Settings.Get<IDocumentStore>();
            var loggingSettings = context.Settings.Get<LoggingSettings>();

            importFailuresHandler = new SatelliteImportFailuresHandler(
                store,
                Path.Combine(loggingSettings.LogPath, @"FailedImports\Error"),
                msg => new FailedErrorImport
                {
                    // TODO: We need a TransportMessage class to resolve this
                    //Message = msg
                    Message = null
                },
                // TODO: How do we get CriticalError?
                null
            );
        }

        private Task OnMessage(IBuilder builder, MessageContext messageContext)
        {
            return builder.Build<ErrorIngestor>().Ingest(messageContext);
        }

        private RecoverabilityAction OnError(RecoverabilityConfig config, ErrorContext errorContext)
        {
            var recoverabilityAction = DefaultRecoverabilityPolicy.Invoke(config, errorContext);

            if (recoverabilityAction is MoveToError)
            {
                importFailuresHandler.Handle(errorContext).GetAwaiter().GetResult();
            }

            return recoverabilityAction;
        }
    }
}