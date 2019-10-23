namespace ServiceControl.Operations
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Transport;
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

            var queueBindings = context.Settings.Get<QueueBindings>();
            queueBindings.BindReceiving(settings.ErrorQueue);

            context.Container.ConfigureComponent<ErrorIngestionCustomCheck.State>(DependencyLifecycle.SingleInstance);
            context.RegisterStartupTask(b => new StartupTask(b.Build<ErrorIngestionComponent>()));
        }

        class StartupTask : FeatureStartupTask
        {
            readonly ErrorIngestionComponent errorIngestion;

            public StartupTask(ErrorIngestionComponent errorIngestion)
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