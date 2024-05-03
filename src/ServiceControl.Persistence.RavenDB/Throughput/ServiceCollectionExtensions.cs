#nullable enable
namespace ServiceControl.Persistence.RavenDB.Throughput;

using Microsoft.Extensions.DependencyInjection;
using Particular.ThroughputCollector.Persistence;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddThroughputRavenPersistence(this IServiceCollection services, string throughputDatabaseName)
    {
        services.AddSingleton(new ThroughputDatabaseConfiguration(throughputDatabaseName));
        services.AddSingleton<IThroughputDataStore, ThroughputDataStore>();

        return services;
    }
}