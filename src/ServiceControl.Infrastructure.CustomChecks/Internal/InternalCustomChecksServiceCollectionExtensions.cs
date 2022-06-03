
namespace ServiceControl.CustomChecks.Internal
{
    using Microsoft.Extensions.DependencyInjection;

    public static class InternalCustomChecksServiceCollectionExtensions
    {
        public static void AddCustomCheck<T>(this IServiceCollection serviceCollection)
            where T : class, ICustomCheck
        {
            serviceCollection.AddTransient<ICustomCheck, T>();
        }
    }
}