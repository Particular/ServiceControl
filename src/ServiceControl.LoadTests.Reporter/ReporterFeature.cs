namespace ServiceControl.LoadTests.Reporter
{
    using NServiceBus;
    using NServiceBus.Features;
    using Operations;
    using ServiceBus.Management.Infrastructure.Settings;

    public class ReporterFeature : Feature
    {
        public ReporterFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var settings = context.Settings.Get<Settings>("ServiceControl.Settings");
            var loadGenetorQueue = SettingsReader<string>.Read("LoadGeneratorQueue");
            var auditQueueAddress = context.Settings.ToTransportAddress(settings.AuditQueue);
            var statistics = new Statistics();

            context.Container.ConfigureComponent(b => new StatisticsEnricher(statistics), DependencyLifecycle.SingleInstance);
            context.RegisterStartupTask(new ReportProcessingStatistics(statistics, auditQueueAddress, loadGenetorQueue));
        }
    }
}