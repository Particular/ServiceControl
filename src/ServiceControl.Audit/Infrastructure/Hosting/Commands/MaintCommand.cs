namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System.Threading.Tasks;

    class MaintCommand : AbstractCommand
    {
        public override Task Execute(HostArguments args) => MaintenanceBootstrapper.Run(args);
    }
}