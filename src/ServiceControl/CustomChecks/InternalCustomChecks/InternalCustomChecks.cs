namespace ServiceControl.CustomChecks
{
    using System.Linq;
    using Infrastructure.BackgroundTasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.CustomChecks;
    using NServiceBus.Hosting;
    using Operations;
    using ServiceBus.Management.Infrastructure.Settings;

    static class InternalCustomChecksServiceCollectionExtensions
    {
        public static void AddCustomCheck<T>(this IServiceCollection serviceCollection)
            where T : class, ICustomCheck
        {
            serviceCollection.AddTransient<ICustomCheck, T>();
        }
    }

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