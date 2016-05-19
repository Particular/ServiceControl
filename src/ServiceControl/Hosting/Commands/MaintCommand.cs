namespace ServiceControl.Hosting.Commands
{
    using Particular.ServiceControl;
    using Particular.ServiceControl.Commands;
    using Particular.ServiceControl.Hosting;

    class MaintCommand : AbstractCommand
    {

        public override void Execute(HostArguments args)
        {
            var bootstrapper = new MaintenanceBootstrapper();
            bootstrapper.Run();
        }
    }
}
