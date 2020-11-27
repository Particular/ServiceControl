using System.Threading.Tasks;
using Particular.ServiceControl;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Infrastructure.RavenDB;

namespace ServiceControl.Hosting.Commands
{
    class SetupCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            var settings = new Settings(args.ServiceName)
            {
                SkipQueueCreation = args.SkipQueueCreation
            };
            var loggingSettings = new LoggingSettings(settings.ServiceName);
            var embeddedDatabase = EmbeddedDatabase.Start(settings.DbPath, loggingSettings.LogPath, settings.RavenDBNetCoreRuntimeVersion, settings.ExpirationProcessTimerInSeconds, settings.DatabaseMaintenanceUrl, settings.RavenBinFolder);
            await new SetupBootstrapper(settings, loggingSettings, embeddedDatabase).Run(args.Username)
                .ConfigureAwait(false);
            embeddedDatabase.Dispose();
        }
    }
}