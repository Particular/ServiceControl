namespace Particular.ServiceControl.Hosting
{
    using System;
    using System.ServiceProcess;
    using NServiceBus;
    using Particular.HealthMonitoring.Uptime;
    using ServiceBus.Management.Infrastructure.Settings;

    public class Host : ServiceBase
    {
        public void Run(bool interactive)
        {
            if (interactive)
            {
                RunInteractive();
            }
            else
            {
                RunAsService();
            }
        }

        private void RunInteractive()
        {
            OnStart(null);
        }

        private void RunAsService()
        {
            Run(this);
        }

        protected override void OnStart(string[] args)
        {
            var busConfiguration = new BusConfiguration();
            busConfiguration.AssembliesToScan(AllAssemblies.Except("ServiceControl.Plugin"));

            var loggingSettings = new LoggingSettings(ServiceName);

            bootstrapper = new Bootstrapper(Stop, new Settings(ServiceName), busConfiguration, loggingSettings, new[]
            {
                new UptimeMonitoring().CreateActivator()
            });
            bootstrapper.Start();
        }

        internal Action OnStopping = () => { };

        protected override void OnStop()
        {
            bootstrapper?.Stop();

            OnStopping();
        }

        protected override void Dispose(bool disposing)
        {
            OnStop();
        }

        Bootstrapper bootstrapper;
    }
}