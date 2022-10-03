namespace ServiceControl.Audit.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;

    public interface IPersistenceConfiguration
    {
        IPersistenceLifecycle ConfigureServices(IServiceCollection serviceCollection, PersistenceSettings settings);
        Task Setup(PersistenceSettings settings, CancellationToken cancellationToken = default);
    }
}