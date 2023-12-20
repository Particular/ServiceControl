namespace Particular.ServiceControl
{
    using global::ServiceControl.Persistence;
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class MaintenanceBootstrapper
    {
        public HostApplicationBuilder HostBuilder { get; set; }

        public MaintenanceBootstrapper(Settings settings)
        {
            HostBuilder = Host.CreateApplicationBuilder();
            HostBuilder.SetupPersistence(settings, maintenanceMode: true);
        }
    }
}