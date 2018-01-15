namespace Particular.ServiceControl.Commands
{
    using Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class SetupCommand : AbstractCommand
    {
        public override void Execute(HostArguments args)
        {
            new SetupBootstrapper(new Settings(args.ServiceName)
            {
                SkipQueueCreation = args.SkipQueueCreation
            }).Run(args.Username);
        }
    }
}