namespace ServiceControl.Connection
{
    using Microsoft.Extensions.DependencyInjection;

    static class ConnectionProvidersServiceCollectionExtensions
    {
        public static void AddPlatformConnectionProvider<T>(this IServiceCollection services)
            where T : class, IProvidePlatformConnectionDetails =>
            services.AddSingleton<IProvidePlatformConnectionDetails, T>();
    }
}