namespace ServiceControl.Persistence
{
    using Microsoft.Extensions.DependencyInjection;

    public interface IPersistence
    {
        void Configure(IServiceCollection serviceCollection);
        void ConfigureInstaller(IServiceCollection serviceCollection);
        void ConfigureLifecycle(IServiceCollection serviceCollection);
    }
}
