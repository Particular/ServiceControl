namespace ServiceControl.Hosting.Commands
{
    using System.Threading.Tasks;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Commands;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class MaintCommand : AbstractCommand
    {
        public override Task Execute(HostArguments args, Settings settings) => MaintenanceBootstrapper.Run(args, settings);
    }
}