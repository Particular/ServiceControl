namespace Particular.ServiceControl.Commands
{
    using Hosting;
    using NServiceBus;

    class SetupCommand : AbstractCommand
    {
        public override void Execute(HostArguments args)
        {
            var configuration = new BusConfiguration();
            configuration.AssembliesToScan(AllAssemblies.Except("ServiceControl.Plugin"));
            configuration.EnableInstallers(args.Username);
            new Bootstrapper(hostArguments: args, configuration:configuration);
        }
    }
}