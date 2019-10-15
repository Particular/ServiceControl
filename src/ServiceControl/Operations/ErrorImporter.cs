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
            var settings = context.Settings.Get<Settings>("ServiceControl.Settings");

            context.Container.ConfigureComponent<ErrorIngestor>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<ErrorPersister>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<FailedMessageAnnouncer>(DependencyLifecycle.SingleInstance);

            context.RegisterStartupTask(b => new StartupTask(b.Build<ErrorIngestion>()));

            if (settings.ForwardErrorMessages)
            {
                context.RegisterStartupTask(b => new EnsureCanWriteToForwardingAddress(b.Build<IForwardMessages>(), settings.ErrorLogQueue));
            }
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