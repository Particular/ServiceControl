
namespace ServiceControl.CustomChecks
{
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.CustomChecks;

    static class InternalCustomChecksServiceCollectionExtensions
    {
        public static void AddCustomCheck<T>(this IServiceCollection serviceCollection)
            where T : class, ICustomCheck
        {
            serviceCollection.AddTransient<ICustomCheck, T>();
        }
    }
}