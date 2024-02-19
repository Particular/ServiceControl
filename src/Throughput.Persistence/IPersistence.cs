namespace Throughput.Persistence;

using Microsoft.Extensions.DependencyInjection;

public interface IPersistence
{
    IPersistenceLifecycle Configure(IServiceCollection serviceCollection);
    IPersistenceInstaller CreateInstaller();
}