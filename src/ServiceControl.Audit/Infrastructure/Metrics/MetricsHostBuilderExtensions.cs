namespace ServiceControl.Audit.Infrastructure.Metrics
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using ServiceControl.Infrastructure.Metrics;

    static class MetricsHostBuilderExtensions
    {
        public static IHostBuilder UseMetrics(this IHostBuilder hostBuilder, bool printMetrics)
        {
            hostBuilder.ConfigureServices(sc =>
            {
                sc.AddSingleton(new Metrics { Enabled = printMetrics });
                sc.AddHostedService<MetricsReporterHostedService>();
            });

            return hostBuilder;
        }
    }
}