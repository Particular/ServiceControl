namespace Particular.ThroughputCollector.Persistence;

using Microsoft.Extensions.DependencyInjection;

public interface IPersistence
{
    PersistenceService Configure(IServiceCollection serviceCollection);
    IPersistenceInstaller CreateInstaller();
}