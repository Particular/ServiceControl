namespace Particular.License
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.License.Contracts;

    public static class ThroughputCollectorHostBuilderExtensions
    {
        public static IHostApplicationBuilder AddThroughputCollector(this IHostApplicationBuilder hostBuilder, string broker, string serviceControlAPI, string auditQueue, string errorQueue)
        {
            var services = hostBuilder.Services;
            services.AddSingleton(new LicenseData { Broker = broker, ServiceControlAPI = serviceControlAPI, AuditQueue = auditQueue, ErrorQueue = errorQueue });
            services.AddHostedService<ThroughputCollectorHostedService>();
            return hostBuilder;
        }
    }
}