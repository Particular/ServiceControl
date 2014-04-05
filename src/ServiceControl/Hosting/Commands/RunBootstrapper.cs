namespace Particular.ServiceControl.Commands
{
    using Hosting;
    using NServiceBus;
    using NServiceBus.Installation.Environments;

    class RunBootstrapper : AbstractCommand
    {
        public override void Execute(HostArguments args)
        {
            new Bootstrapper();
            Configure.Instance.ForInstallationOn<Windows>().Install();
        }
    }
}