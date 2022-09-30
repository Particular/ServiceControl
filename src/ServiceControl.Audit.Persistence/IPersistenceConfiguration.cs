namespace ServiceControl.Audit.Persistence
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;

    public interface IPersistenceConfiguration
    {
        void ConfigureServices(IServiceCollection serviceCollection, PersistenceSettings settings);
        Task Setup(PersistenceSettings settings);
    }
}