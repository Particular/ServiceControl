namespace ServiceControl.Operations
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
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
            context.Container.ConfigureComponent<ErrorIngestor>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<ErrorPersister>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<FailedMessageAnnouncer>(DependencyLifecycle.SingleInstance);
            context.RegisterStartupTask(b => new StartupTask(b.Build<ErrorIngestion>()));
        }

        class StartupTask : FeatureStartupTask
        {
            readonly ErrorIngestion errorIngestion;

            public StartupTask(ErrorIngestion errorIngestion)
            {
                this.errorIngestion = errorIngestion;
            }

            protected override Task OnStart(IMessageSession session)
            {
                return errorIngestion.Start();
            }

            protected override Task OnStop(IMessageSession session)
            {
                return errorIngestion.Stop();
            }
        }
    }
}