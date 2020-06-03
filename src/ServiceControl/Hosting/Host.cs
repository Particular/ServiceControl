namespace Particular.ServiceControl.Hosting
{
    using System;
    using System.ServiceProcess;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

    class Host : ServiceBase
    {
        public Host(bool logToConsole)
        {
            this.logToConsole = logToConsole;
        }

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

            var loggingSettings = new LoggingSettings(ServiceName, logToConsole);

            var settings = new Settings(ServiceName)
            {
                RunCleanupBundle = true
            };
            bootstrapper = new Bootstrapper(settings, busConfiguration, loggingSettings);
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

        bool logToConsole;
        Bootstrapper bootstrapper;
    }
}
