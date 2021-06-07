namespace ServiceControl.Infrastructure.Metrics
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;

    class MetricsComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(sc =>
            {
                sc.AddSingleton(new Metrics { Enabled = settings.PrintMetrics });
                sc.AddHostedService<MetricsReporterHostedService>();
            });
        }

        public override void Setup(Settings settings, IComponentSetupContext context)
        {
        }
    }

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
