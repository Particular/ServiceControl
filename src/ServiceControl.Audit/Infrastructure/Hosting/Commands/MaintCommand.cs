namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System.Threading.Tasks;

    class MaintCommand : AbstractCommand
    {
        public override Task Execute(HostArguments args)
        {
            var bootstrapper = new MaintenanceBootstrapper();
            return bootstrapper.Run(args);
        }
    }
}