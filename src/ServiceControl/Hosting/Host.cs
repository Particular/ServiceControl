namespace Particular.ServiceControl.Hosting
{
    using System;
    using System.ServiceProcess;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

    class Host : ServiceBase
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

        void RunInteractive()
        {
            OnStart(null);
        }

        void RunAsService()
        {
            Run(this);
        }

        protected override void OnStart(string[] args)
        {
            var busConfiguration = new EndpointConfiguration(ServiceName);
            var assemblyScanner = busConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            var loggingSettings = new LoggingSettings(ServiceName);

            var settings = new Settings(ServiceName)
            {
                RunCleanupBundle = true
            };
            bootstrapper = new Bootstrapper(ctx => Stop(), settings, busConfiguration, loggingSettings);
            bootstrapper.Start().GetAwaiter().GetResult();
        }

        protected override void OnStop()
        {
            bootstrapper?.Stop().GetAwaiter().GetResult();

            OnStopping();
        }

        protected override void Dispose(bool disposing)
        {
            OnStop();
            bootstrapper?.Dispose();
        }

        internal Action OnStopping = () => { };

        Bootstrapper bootstrapper;
    }
}