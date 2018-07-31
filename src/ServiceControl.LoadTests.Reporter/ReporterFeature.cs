namespace ServiceControl.LoadTests.Reporter
{
    using System;
    using Metrics;
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
            var metricsContext = new DefaultMetricsContext();
            var metricsConfig = new MetricsConfig(metricsContext);
            metricsConfig.WithReporting(r =>
            {
                r.WithCSVReports(".", TimeSpan.FromSeconds(5));
            });

            var processedMeter = metricsContext.Meter("Processed", Unit.Custom("audits"), TimeUnit.Seconds, default);

            var settings = context.Settings.Get<Settings>("ServiceControl.Settings");
            var loadGenetorQueue = SettingsReader<string>.Read("LoadGeneratorQueue");
            var auditQueueAddress = context.Settings.ToTransportAddress(settings.AuditQueue);
            var statistics = new Statistics();

            context.Container.ConfigureComponent(b => new StatisticsEnricher(statistics, processedMeter), DependencyLifecycle.SingleInstance);
            context.RegisterStartupTask(new ReportProcessingStatistics(statistics, auditQueueAddress, loadGenetorQueue));
        }
    }
}