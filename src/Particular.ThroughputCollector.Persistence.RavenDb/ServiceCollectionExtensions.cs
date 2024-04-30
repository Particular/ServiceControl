namespace Particular.ThroughputCollector.Persistence.RavenDb;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Configuration;

public static class ServiceCollectionExtensions
{
    const string DatabaseNameKey = "RavenDB/ThroughputDatabaseName";
    const string DefaultDatabaseName = "throughput";

    public static IServiceCollection AddThroughputRavenPersistence(this IServiceCollection services)
    {
        var databaseConfiguration = new DatabaseConfiguration(SettingsReader.Read(
            new SettingsRootNamespace(SettingsHelper.SettingsNamespace), DatabaseNameKey, DefaultDatabaseName));

        services.AddSingleton(databaseConfiguration);
        services.AddSingleton<IThroughputDataStore, ThroughputDataStore>();
        services.AddSingleton<IPersistenceInstaller, RavenInstaller>(provider => new RavenInstaller(provider));

        return services;
    }
}