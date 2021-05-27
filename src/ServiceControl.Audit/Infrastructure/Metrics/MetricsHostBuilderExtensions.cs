namespace ServiceControl.Audit.Infrastructure.Metrics
{
    using System;
    using System.Collections.Generic;
    using Autofac;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using ServiceControl.Infrastructure.Metrics;

    static class MetricsHostBuilderExtensions
    {
        public static IHostBuilder UseMetrics(this IHostBuilder hostBuilder,
            List<Action<ContainerBuilder>> registrations, bool printMetrics)
        {
            registrations.Add(cb => cb.RegisterInstance(new Metrics { Enabled = printMetrics }).ExternallyOwned());

            hostBuilder.ConfigureServices(sc => { sc.AddHostedService<MetricsReporterHostedService>(); });

            return hostBuilder;
        }
    }
}