using System;
using System.Threading.Tasks;
using NServiceBus.Logging;
using ServiceControl.Infrastructure.RavenDB;

namespace ServiceControl.Audit.Infrastructure.Hosting
{
    using System.ServiceProcess;
    using Raven.Client.Documents;
    using Settings;

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

            embeddedDatabase = EmbeddedDatabase.Start(settings.DbPath, loggingSettings.LogPath, settings.ExpirationProcessTimerInSeconds, settings.DatabaseMaintenanceUrl);
            await embeddedDatabase.PrepareDatabase(new AuditDatabaseConfiguration()).ConfigureAwait(false);
        }

        public Action OnStopping { get; set; }

        EmbeddedDatabase embeddedDatabase;
    }
}