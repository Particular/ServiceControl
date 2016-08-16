namespace Particular.ServiceControl.Hosting
{
    using System;
    using System.ServiceProcess;
    using Microsoft.Owin.Hosting;
    using ServiceBus.Management.Infrastructure.OWIN;
    using ServiceBus.Management.Infrastructure.Settings;

    public class MaintenanceHost : ServiceBase
    {
        private readonly Settings settings;

        public MaintenanceHost(Settings settings)
        {
            this.settings = settings;
            ServiceName = settings.ServiceName;
        }

        public void Run()
        {
            Run(this);
        }

        private IDisposable stop;

        protected override void OnStart(string[] args)
        {
            var startup = new Startup(null, this, settings, null, null, null);
            stop = WebApp.Start(new StartOptions(settings.RootUrl), startup.ConfigureRavenDB);
        }


        protected override void OnStop()
        {
            stop?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            OnStop();
        }

    }
}