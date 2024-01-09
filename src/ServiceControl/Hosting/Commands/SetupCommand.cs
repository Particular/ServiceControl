namespace Particular.ServiceControl.Commands
{
    using System.Threading.Tasks;
    using Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class SetupCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            settings.SkipQueueCreation = args.SkipQueueCreation;
            // TODO: Update to use the same pattern as the main Bootstrapper
            await new SetupBootstrapper(settings).Run();
        }
    }
}