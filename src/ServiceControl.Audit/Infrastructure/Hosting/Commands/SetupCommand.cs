using ServiceControl.Infrastructure.RavenDB;

namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System.Threading.Tasks;
    using Settings;

    class SetupCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            var settings = new Settings(args.ServiceName)
            {
                SkipQueueCreation = args.SkipQueueCreation
            };
            var loggingSettings = new LoggingSettings(settings.ServiceName);
            var embeddedDatabase = EmbeddedDatabase.Start(settings.DbPath, loggingSettings.LogPath, settings.ExpirationProcessTimerInSeconds, settings.DatabaseMaintenanceUrl);

            await new SetupBootstrapper(settings, loggingSettings, embeddedDatabase).Run(args.Username)
                .ConfigureAwait(false);
            embeddedDatabase.Dispose();
        }
    }
}