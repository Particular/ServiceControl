namespace Particular.ServiceControl.Commands
{
    using Hosting;

    class SetupCommand : AbstractCommand
    {
        public override void Execute(HostArguments args)
        {
            new SetupBootstrapper().Run(args.Username);
        }
    }
}