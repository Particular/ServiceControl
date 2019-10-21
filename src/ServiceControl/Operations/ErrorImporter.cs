namespace ServiceControl.Operations
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.CustomChecks;
    using NServiceBus.Features;
    using NServiceBus.Transport;
    using ServiceBus.Management.Infrastructure.Settings;

    class ErrorIngestionCustomCheck : CustomCheck
    {
        readonly CriticalErrorHolder criticalErrorHolder;

        public ErrorIngestionCustomCheck(CriticalErrorHolder criticalErrorHolder) 
            : base("Failed message ingestion process", "ServiceControl", TimeSpan.FromSeconds(5))
        {
            this.criticalErrorHolder = criticalErrorHolder;
        }

        public override Task<CheckResult> PerformCheck()
        {
            var failure = criticalErrorHolder.GetLastFailure();
            if (failure == null)
            {
                return Task.FromResult(CheckResult.Pass);
            }

            return Task.FromResult(CheckResult.Failed(failure));
        }
    }

    class CriticalErrorHolder
    {
        volatile string lastFailure;

        public void Clear() => lastFailure = null;
        public void ReportError(string failure) => lastFailure = failure;
        public string GetLastFailure()
        {
            return lastFailure;
        }
    }

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

            context.Container.ConfigureComponent<ErrorIngestor>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<ErrorPersister>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<FailedMessageAnnouncer>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<CriticalErrorHolder>(DependencyLifecycle.SingleInstance);
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