namespace ServiceControl.Audit.Infrastructure.Hosting
{
    using System;
    using System.ServiceProcess;
    using NServiceBus;
    using Settings;

    class Host : ServiceBase
    {
        public void RunAsConsole()
        {
            OnStart(null);
        }

        public void RunAsService()
        {
            ServiceBase.Run(this);
        }

        protected override void OnStart(string[] args)
        {
            var busConfiguration = new EndpointConfiguration(ServiceName);
            var assemblyScanner = busConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            var settings = new Settings(ServiceName)
            {
                RunCleanupBundle = true
            };
            bootstrapper = new Bootstrapper(
                ctx => { }, //Do nothing. The transports in NSB 7 are designed to handle broker outages. Audit ingestion will be paused when broker is unavailable.
                settings, busConfiguration, LoggingConfigurator.Settings);
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
        }

        internal Action OnStopping = () => { };

        Bootstrapper bootstrapper;
    }
}