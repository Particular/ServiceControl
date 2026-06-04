namespace ServiceControl.CustomChecks
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using NServiceBus.CustomChecks;

    public static class InternalCustomChecksServiceCollectionExtensions
    {
        public static void AddCustomCheck<T>(this IServiceCollection services)
            where T : class, ICustomCheck =>
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ICustomCheck, T>());
    }
}