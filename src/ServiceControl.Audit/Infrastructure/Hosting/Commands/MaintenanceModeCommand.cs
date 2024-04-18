namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Persistence;
    using Settings;

    class MaintenanceModeCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration(settings);
            var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings(settings);

            persistenceSettings.MaintenanceMode = true;

            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.Services.AddPersistence(persistenceSettings, persistenceConfiguration);

            hostBuilder.Services.AddWindowsService();

            var host = hostBuilder.Build();
            await host.RunAsync();
        }
    }
}