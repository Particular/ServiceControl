namespace Particular.ServiceControl.Hosting
{
    using System;
    using System.ServiceProcess;
    using Microsoft.Owin.Hosting;
    using ServiceBus.Management.Infrastructure.OWIN;
    using ServiceBus.Management.Infrastructure.Settings;

    public class MaintenanceHost : ServiceBase
    {
        public void Run()
        {
            Run(this);
        }

        private IDisposable stop;

        protected override void OnStart(string[] args)
        {
            var startup = new Startup(null);
            stop = WebApp.Start(new StartOptions(Settings.RootUrl), startup.ConfigureRavenDB);
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