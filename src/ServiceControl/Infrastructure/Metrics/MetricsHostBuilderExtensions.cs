namespace ServiceControl.Infrastructure.Metrics
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    static class MetricsHostBuilderExtensions
    {
        public static IHostApplicationBuilder UseMetrics(this IHostApplicationBuilder hostBuilder, bool printMetrics)
        {
            var services= hostBuilder.Services;
            services.AddSingleton(new Metrics { Enabled = printMetrics });
            services.AddHostedService<MetricsReporterHostedService>();
            return hostBuilder;
        }
    }
}
