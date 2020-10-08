namespace Particular.ServiceControl.Hosting
{
    using System;
    using System.ServiceProcess;
    using global::ServiceControl.Infrastructure;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

    class Host : ServiceBase
    {
        public void Start()
        {
            OnStart(null);
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
            embeddedDatabase = EmbeddedDatabase.Start(settings, loggingSettings);
            bootstrapper = new Bootstrapper(settings, busConfiguration, loggingSettings, embeddedDatabase);
            bootstrapper.Start().GetAwaiter().GetResult();
        }

        protected override void OnStop()
        {
            bootstrapper?.Stop().GetAwaiter().GetResult();
            embeddedDatabase?.Dispose();
            OnStopping();
        }

        protected override void Dispose(bool disposing)
        {
            OnStop();
        }

        internal Action OnStopping = () => { };

        Bootstrapper bootstrapper;
        EmbeddedDatabase embeddedDatabase;
    }
}