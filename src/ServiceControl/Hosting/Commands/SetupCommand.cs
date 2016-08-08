namespace Particular.ServiceControl.Commands
{
    using Hosting;
    using Microsoft.Owin.Hosting;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

    class SetupCommand : AbstractCommand
    {
        public override void Execute(HostArguments args)
        {
            var configuration = new BusConfiguration();
            configuration.AssembliesToScan(AllAssemblies.Except("ServiceControl.Plugin"));
            configuration.EnableInstallers(args.Username);
            var settings = new Settings(args.ServiceName) { SetupOnly  = true };
            var bootstrap = new Bootstrapper(settings, configuration);

            var startOptions = new StartOptions(settings.RootUrl);
            WebApp.Start(startOptions, bootstrap.Startup.Configuration).Dispose();
        }
    }
}