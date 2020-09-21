using System;
using System.ServiceProcess;
using System.Threading.Tasks;
using NServiceBus;
using ServiceControl.Audit.Infrastructure.Settings;
using ServiceControl.Infrastructure.RavenDB;

namespace ServiceControl.Audit.Infrastructure.Hosting
{
    class Host : ServiceBase, IStartableStoppableService
    {
        public async Task Start()
        {
            var busConfiguration = new EndpointConfiguration(ServiceName);
            var assemblyScanner = busConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            var loggingSettings = new LoggingSettings(ServiceName);
            var settings = new Settings.Settings(ServiceName);
            embeddedDatabase = EmbeddedDatabase.Start(settings.DbPath, loggingSettings.LogPath, settings.RavenDBNetCoreRuntimeVersion, settings.ExpirationProcessTimerInSeconds, settings.DatabaseMaintenanceUrl);
            bootstrapper = new Bootstrapper(
                ctx => { }, //Do nothing. The transports in NSB 7 are designed to handle broker outages. Audit ingestion will be paused when broker is unavailable.
                settings, busConfiguration, loggingSettings, embeddedDatabase);
            await bootstrapper.Start().ConfigureAwait(false);
        }
        public Action OnStopping { get; set; } = () => { };

        protected override void OnStart(string[] args)
        {
            Start().GetAwaiter().GetResult();
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

        Bootstrapper bootstrapper;
        EmbeddedDatabase embeddedDatabase;
    }
}