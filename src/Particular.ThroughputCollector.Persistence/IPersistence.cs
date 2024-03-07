namespace Particular.ThroughputCollector.Persistence;

using Microsoft.Extensions.DependencyInjection;

public interface IPersistence
{
    IServiceCollection Configure(IServiceCollection serviceCollection);
}