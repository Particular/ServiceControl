namespace Particular.ServiceControl.Commands
{
    using Hosting;

    class RunBootstrapperAndNServiceBusInstallers : AbstractCommand
    {
        public override void Execute(HostArguments args)
        {
            new Bootstrapper(null,true,args.Username);
        }
    }
}