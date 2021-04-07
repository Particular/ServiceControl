namespace ServiceControl.Audit.Infrastructure.Hosting
{
    using System.ServiceProcess;
    using Settings;

    class MaintenanceHost : ServiceBase
    {
        public MaintenanceHost(Settings settings) => ServiceName = settings.ServiceName;

        public void Run() => Run(this);
    }
}