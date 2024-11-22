namespace ServiceControl.Infrastructure.Metrics
{
    using Microsoft.Extensions.DependencyInjection;

    static class MetricsServiceCollectionExtensions
    {
        public static void AddMetrics(this IServiceCollection services, bool printMetrics)
        {
            services.AddSingleton(new Metrics { Enabled = printMetrics });
            services.AddHostedService<MetricsReporterHostedService>();
        }
    }
}