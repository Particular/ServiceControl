namespace ServiceControl.Persistence
{
    using Microsoft.Extensions.DependencyInjection;

    public interface IPersistence
    {
        void Configure(IServiceCollection serviceCollection);
        IPersistenceInstaller CreateInstaller();
        void ConfigureLifecycle(IServiceCollection serviceCollection);
    }
}
