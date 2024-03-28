namespace ServiceControl.Audit.Persistence.RavenDB
{
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.CustomChecks;

    static class InternalCustomChecksServiceCollectionExtensions
    {
        public static void AddCustomCheck<T>(this IServiceCollection serviceCollection)
            where T : class, ICustomCheck
        {
            serviceCollection.AddTransient<T>(); // Allows for T to have different instance registered for testing
            serviceCollection.AddTransient<ICustomCheck, T>(b => b.GetService<T>());
        }
    }
}