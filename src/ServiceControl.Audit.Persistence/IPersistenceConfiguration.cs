namespace ServiceControl.Audit.Persistence
{
    using Microsoft.Extensions.DependencyInjection;

    public interface IPersistenceConfiguration
    {
        void ConfigureServices(IServiceCollection serviceCollection, PersistenceSettings settings);
    }
}