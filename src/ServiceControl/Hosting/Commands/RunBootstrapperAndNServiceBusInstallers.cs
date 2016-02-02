namespace Particular.ServiceControl.Commands
{
    using Hosting;
    using NServiceBus;

    class RunBootstrapperAndNServiceBusInstallers : AbstractCommand
    {
        public override void Execute(HostArguments args)
        {
            var configuration = new BusConfiguration();
            configuration.ExcludeAssemblies("ServiceControl.Plugin");
            configuration.EnableInstallers(args.Username);
            new Bootstrapper(hostArguments: args, configuration:configuration);
        }
    }
}