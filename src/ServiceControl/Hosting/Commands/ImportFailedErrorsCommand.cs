namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.Logging;
    using Operations;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class ImportFailedErrorsCommand : AbstractCommand
    {
        readonly ILog Log = LogManager.GetLogger<ImportFailedErrorsCommand>();

        public override async Task Execute(HostArguments args, Settings settings)
        {
            settings.IngestErrorMessages = false;
            settings.RunRetryProcessor = false;
            settings.DisableHealthChecks = true;

            EndpointConfiguration endpointConfiguration = CreateEndpointConfiguration(settings);

            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.AddServiceControl(settings, endpointConfiguration);
            hostBuilder.AddServiceControlApi();

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
                Log.Info("Cancelled", e);
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