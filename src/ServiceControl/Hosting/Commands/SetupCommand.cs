namespace Particular.ServiceControl.Commands
{
    using System.Threading.Tasks;
    using global::ServiceControl.Infrastructure;
    using Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class SetupCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            var settings = new Settings(args.ServiceName)
            {
                SkipQueueCreation = args.SkipQueueCreation
            };
            var loggingSettings = new LoggingSettings(settings.ServiceName);
            var embeddedDatabase = EmbeddedDatabase.Start(settings, loggingSettings);
            await new SetupBootstrapper(settings, loggingSettings, embeddedDatabase).Run(args.Username)
                .ConfigureAwait(false);
            embeddedDatabase.Dispose();
        }
    }
}