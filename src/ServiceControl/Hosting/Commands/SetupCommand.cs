namespace Particular.ServiceControl.Commands
{
    using System.Threading.Tasks;
    using Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class SetupCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            await new SetupBootstrapper(new Settings(args.ServiceName)
            {
                SkipQueueCreation = args.SkipQueueCreation,
                Components = Components.All
            }).Run(args.Username)
                .ConfigureAwait(false);
        }
    }
}