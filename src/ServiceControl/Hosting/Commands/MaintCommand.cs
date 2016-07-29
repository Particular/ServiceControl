namespace ServiceControl.Hosting.Commands
{
    using Particular.ServiceControl;
    using Particular.ServiceControl.Commands;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class MaintCommand : AbstractCommand
    {
        Bootstrapper bootstrapper;

        public override void Execute(HostArguments args)
        {
            bootstrapper = new Bootstrapper(new Settings(args.ServiceName)
            {
                MaintenanceMode = true
            });
            bootstrapper.Start();
        }
    }
}
