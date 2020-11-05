using System;
using System.Threading.Tasks;
using NServiceBus.Logging;
using ServiceControl.Hosting;
using ServiceControl.Infrastructure.RavenDB;
using ServiceControl.Infrastructure.RavenDB.Subscriptions;

namespace Particular.ServiceControl.Hosting
{
    using System.ServiceProcess;
    using ServiceBus.Management.Infrastructure.Settings;

    class MaintenanceHost : ServiceBase, IStartableStoppableService
    {
        protected override void OnStart(string[] args)
        {
            Start().GetAwaiter().GetResult();
        }

        protected override void OnStop()
        {
            embeddedDatabase?.Dispose();
            OnStopping();
        }

        protected override void Dispose(bool disposing)
        {
            OnStop();
        }

        public async Task Start()
        {
            var loggingSettings = new LoggingSettings(ServiceName);
            var settings = new Settings(ServiceName);

            var logger = LogManager.GetLogger(typeof(MaintenanceHost));
            logger.Info($"RavenDB is now accepting requests on {settings.StorageUrl}");

            embeddedDatabase = EmbeddedDatabase.Start(settings.DbPath, loggingSettings.LogPath, settings.RavenDBNetCoreRuntimeVersion, settings.ExpirationProcessTimerInSeconds, settings.DatabaseMaintenanceUrl);
            await embeddedDatabase.PrepareDatabase("servicecontrol", LegacyDocumentConversion.ConventionsFindClrType).ConfigureAwait(false);
        }

        public Action OnStopping { get; set; }

        EmbeddedDatabase embeddedDatabase;
    }
}