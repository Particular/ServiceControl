namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence;
    using Settings;

    class SetupCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            var settings = new Settings(args.ServiceName)
            {
                SkipQueueCreation = args.SkipQueueCreation
            };

            var persistenceSettings = new PersistenceSettings(settings.AuditRetentionPeriod, settings.EnableFullTextSearchOnBodies, settings.MaxBodySizeToStore)
            {
                IsSetup = true
            };

            await new SetupBootstrapper(settings, persistenceSettings).Run(args.Username)
                .ConfigureAwait(false);
        }
    }
}