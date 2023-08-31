namespace Particular.ServiceControl
{
    using global::ServiceControl.Persistence;
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class MaintenanceBootstrapper
    {
        public IHostBuilder HostBuilder { get; set; }

        public MaintenanceBootstrapper(Settings settings) =>
            HostBuilder = new HostBuilder()
                .SetupPersistence(settings, maintenanceMode: true);
    }
}