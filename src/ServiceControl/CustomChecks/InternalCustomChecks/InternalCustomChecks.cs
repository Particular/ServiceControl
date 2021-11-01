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
        public static IHostBuilder UseInternalCustomChecks(this IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddCustomCheck<CriticalErrorCustomCheck>();
                collection.AddCustomCheck<CheckRemotes>();
                collection.AddCustomCheck<CheckFreeDiskSpace>();
                collection.AddCustomCheck<FailedAuditImportCustomCheck>();
                collection.AddCustomCheck<AuditRetentionCheck>();

                collection.AddHostedService(provider => new InternalCustomChecksHostedService(
                    provider.GetServices<ICustomCheck>().ToList(),
                    provider.GetRequiredService<CustomChecksStorage>(),
                    provider.GetRequiredService<HostInformation>(),
                    provider.GetRequiredService<IAsyncTimer>(),
                    provider.GetRequiredService<Settings>().ServiceName));
            });
            return hostBuilder;
        }
    }
}