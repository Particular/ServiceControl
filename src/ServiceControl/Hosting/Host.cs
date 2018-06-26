namespace Particular.ServiceControl.Hosting
{
    using System;
    using System.ServiceProcess;
    using NServiceBus;
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
            var busConfiguration = new EndpointConfiguration(ServiceName);
            var assemblyScanner = busConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            var loggingSettings = new LoggingSettings(ServiceName);

            bootstrapper = new Bootstrapper(Stop, new Settings(ServiceName), busConfiguration, loggingSettings);
            bootstrapper.Start().GetAwaiter().GetResult();
        }

        internal Action OnStopping = () => { };

        protected override void OnStop()
        {
            bootstrapper?.Stop().GetAwaiter().GetResult();

            OnStopping();
        }

        protected override void Dispose(bool disposing)
        {
            OnStop();
        }

        Bootstrapper bootstrapper;
    }
}