namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System.Threading.Tasks;
    using Settings;

    class SetupCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            settings.SkipQueueCreation = args.SkipQueueCreation;

            await new SetupBootstrapper(settings).Run(args.Username)
                .ConfigureAwait(false);
        }
    }
}