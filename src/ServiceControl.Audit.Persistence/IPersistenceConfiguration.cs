namespace ServiceControl.Audit.Persistence
{
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Infrastructure.Settings;

    interface IPersistenceConfiguration
    {
        void ConfigureServices(IServiceCollection serviceCollection, Settings settings, bool maintenanceMode, bool isSetup);
    }
}