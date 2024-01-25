namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure;
    using Microsoft.AspNetCore.Builder;
    using NServiceBus;

    class RunCommand : AbstractCommand
    {
        public override async Task Execute(Settings settings)
        {
            var endpointConfiguration = new EndpointConfiguration(settings.ServiceName);
            settings.RunAsWindowsService = !Environment.UserInteractive && !settings.Portable;

            var hostBuilder = WebApplication.CreateBuilder();
            hostBuilder.AddServiceControlMonitoring((_, __) => Task.CompletedTask, settings, endpointConfiguration);
            var app = hostBuilder.Build();

            app.UseServiceControlMonitoring();
            await app.RunAsync();
        }
    }
}