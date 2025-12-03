namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using Operations;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;

    class ImportFailedErrorsCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            settings.IngestErrorMessages = false;
            settings.RunRetryProcessor = false;
            settings.DisableHealthChecks = true;

            EndpointConfiguration endpointConfiguration = CreateEndpointConfiguration(settings);

            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.AddServiceControl(settings, endpointConfiguration);
            hostBuilder.AddServiceControlApi(settings.CorsSettings);

            using var app = hostBuilder.Build();
            await app.StartAsync();

            var importFailedErrors = app.Services.GetRequiredService<ImportFailedErrors>();

            using var tokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, _) => tokenSource.Cancel();

            try
            {
                await importFailedErrors.Run(tokenSource.Token);
            }
            catch (OperationCanceledException e) when (tokenSource.IsCancellationRequested)
            {
                LoggerUtil.CreateStaticLogger<ImportFailedErrorsCommand>().LogInformation(e, "Cancelled");
            }
            finally
            {
                await app.StopAsync(CancellationToken.None);
            }
        }

        protected virtual EndpointConfiguration CreateEndpointConfiguration(Settings settings)
        {
            var endpointConfiguration = new EndpointConfiguration(settings.InstanceName);
            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            return endpointConfiguration;
        }
    }
}