﻿namespace ServiceControl.LoadTests.Reporter
{
    using System;
    using Audit.Infrastructure;
    using Audit.Infrastructure.Settings;
    using Configuration;
    using Metrics;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.Features;

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
            var loadGenetorQueue = SettingsReader.Read("LoadGeneratorQueue");
            var auditQueueAddress = context.Settings.ToTransportAddress(settings.AuditQueue);
            var statistics = new Statistics();

            context.Services.AddSingleton(new StatisticsEnricher(statistics, processedMeter));
            context.RegisterStartupTask(new ReportProcessingStatistics(statistics, auditQueueAddress, loadGenetorQueue));
        }
    }
}