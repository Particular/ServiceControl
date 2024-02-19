namespace Particular.ServiceControl.Licensing
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.License;
    using Particular.License.Contracts;

    static class ThroughputCollectorHostBuilderExtensions
    {
        public static IHostApplicationBuilder AddThroughputCollector(this IHostApplicationBuilder hostBuilder)
        {
            var services = hostBuilder.Services;
            services.AddSingleton(new LicenseData { Broker = "TODO", ServiceControlAPI = "TODO", AuditQueue = "TODO", ErrorQueue = "TODO" });
            services.AddHostedService<ThroughputCollectorHostedService>();
            return hostBuilder;
        }
    }
}