namespace ServiceControl.Connection
{
    using Microsoft.Extensions.DependencyInjection;

    static class ConnectionProvidersServiceCollectionExtensions
    {
        public static void AddPlatformConnectionProvider<T>(this IServiceCollection serviceCollection)
            where T : class, IProvidePlatformConnectionDetails
        {
            serviceCollection.AddSingleton<IProvidePlatformConnectionDetails, T>();
        }
    }
}
