namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;

    class RunCommand : AbstractCommand
    {
        public override async Task Execute(Settings settings)
        {
            //RunAsWindowsService can't be a property on Settings class because it
            //would be exposed as app.config configurable option and break ATT approvals
            var runAsWindowsService = !Environment.UserInteractive && !settings.Portable;
            var configuration = new EndpointConfiguration(settings.ServiceName);

            var hostBuilder = new Bootstrapper(_ => { }, settings, configuration).HostBuilder;

            if (runAsWindowsService)
            {
                hostBuilder.UseWindowsService();
            }
            else
            {
                hostBuilder.UseConsoleLifetime();
            }

            using (var host = hostBuilder.Build())
            {
                await host.RunAsync();
            }
        }
    }
}