namespace ServiceControl.CustomChecks
{
    using System.Linq;
    using Infrastructure.BackgroundTasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.CustomChecks;
    using NServiceBus.Hosting;
    using Operations;
    using SagaAudit;
    using ServiceBus.Management.Infrastructure.Settings;

    static class InternalCustomChecks
    {
        public static IHostApplicationBuilder AddInternalCustomChecks(this IHostApplicationBuilder hostBuilder)
        {
            var services = hostBuilder.Services;
            services.AddCustomCheck<CriticalErrorCustomCheck>();
            services.AddCustomCheck<CheckRemotes>();
            services.AddCustomCheck<SagaAuditMisconfigurationCustomCheck>();

            services.AddHostedService(provider => new InternalCustomChecksHostedService(
                provider.GetServices<ICustomCheck>().ToList(),
                provider.GetRequiredService<HostInformation>(),
                provider.GetRequiredService<IAsyncTimer>(),
                provider.GetRequiredService<CustomCheckResultProcessor>(),
                provider.GetRequiredService<Settings>().InstanceName));
            return hostBuilder;
        }
    }
}