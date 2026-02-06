namespace Particular.LicensingComponent;

using Contracts;
using Microsoft.Extensions.DependencyInjection;

public static class LicensingComponentServiceCollectionExtensions
{
    public static IServiceCollection AddEnvironmentDataProvider<T>(this IServiceCollection services)
        where T : class, IEnvironmentDataProvider
        => services.AddSingleton<IEnvironmentDataProvider, T>();
}
