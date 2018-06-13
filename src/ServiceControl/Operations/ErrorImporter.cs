namespace ServiceControl.Operations
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Transport;
    using ServiceBus.Management.Infrastructure.Settings;

    class ErrorImporter : Feature
    {
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

                context.AddSatelliteReceiver(
                    "Error Queue Ingestor", 
                    settings.ErrorQueue, 
                    new PushRuntimeSettings(settings.MaximumConcurrencyLevel),
                    OnError, 
                    OnMessage);
            }

            // TODO: Fail startup if can't write to audit forwarding queue but forwarding is enabled
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
                // TODO: Hand off to SatelliteImportFailuresHandler
            }

            return recoverabilityAction;
        }
    }
}