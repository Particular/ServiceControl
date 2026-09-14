namespace ServiceControl.CustomChecks
{
    using System.Linq;
    using Infrastructure.BackgroundTasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
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

            // A host that reports to a primary registers its own reporter before this runs, so the
            // local one only fills the gap.
            services.TryAddSingleton<ICustomCheckResultReporter, LocalCustomCheckResultReporter>();

            services.AddHostedService(provider => new InternalCustomChecksHostedService(
                [.. provider.GetServices<ICustomCheck>()],
                provider.GetRequiredService<HostInformation>(),
                provider.GetRequiredService<IAsyncTimer>(),
                provider.GetRequiredService<ICustomCheckResultReporter>(),
                provider.GetRequiredService<Settings>().InstanceName,
                provider.GetRequiredService<ILogger<InternalCustomChecksHostedService>>()));
            return hostBuilder;
        }
    }
}