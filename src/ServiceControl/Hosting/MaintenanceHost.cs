namespace Particular.ServiceControl.Hosting
{
    using System.ServiceProcess;
    using ServiceBus.Management.Infrastructure.Settings;

    class MaintenanceHost : ServiceBase
    {
        public MaintenanceHost(Settings settings) => ServiceName = settings.ServiceName;

        public void Run() => Run(this);
    }
}