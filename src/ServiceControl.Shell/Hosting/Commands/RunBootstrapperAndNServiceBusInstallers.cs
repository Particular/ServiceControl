namespace Particular.ServiceControl.Commands
{
    using Hosting;
    using NServiceBus.Config;

    class RunBootstrapperAndNServiceBusInstallers : AbstractCommand
    {
        public override void Execute(HostArguments args)
        {
            WindowsInstallerRunner.RunInstallers = true;
            WindowsInstallerRunner.RunAs = args.Username;
            new Bootstrapper(null, args);
        }
    }
}