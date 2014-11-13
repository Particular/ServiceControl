namespace ServiceControl.Hosting.Commands
{
    using Particular.ServiceControl;
    using Particular.ServiceControl.Commands;
    using Particular.ServiceControl.Hosting;

    class MaintCommand : AbstractCommand
    {
        Bootstrapper bootstrapper;

        public override void Execute(HostArguments args)
        {
            bootstrapper = new Bootstrapper(null, args);
            bootstrapper.Start();
        }
    }
}
