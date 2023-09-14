namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NLog;
    using NServiceBus;
    using Operations;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Commands;
    using Particular.ServiceControl.Hosting;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;

    class ImportFailedErrorsCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            settings.IngestErrorMessages = false;
            settings.RunRetryProcessor = false;
            settings.DisableHealthChecks = true;

            EndpointConfiguration busConfiguration = CreateEndpointConfiguration(settings);

            var loggingSettings = new LoggingSettings(settings.ServiceName, LogLevel.Info, LogLevel.Info);
            var bootstrapper = new Bootstrapper(settings, busConfiguration, loggingSettings);
            var host = bootstrapper.HostBuilder.Build();

            var lifeCycle = host.Services.GetRequiredService<IPersistenceLifecycle>();
            await lifeCycle.Start(); // Initialized IDocumentStore, this is needed as many hosted services have (indirect) dependencies on it.

            await host.StartAsync(CancellationToken.None);

            var importFailedErrors = host.Services.GetRequiredService<ImportFailedErrors>();

            using (var tokenSource = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (sender, eventArgs) => { tokenSource.Cancel(); };

                try
                {
                    await importFailedErrors.Run(tokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // no-op
                }
                finally
                {
                    await host.StopAsync(CancellationToken.None);
                    await lifeCycle.Stop();
                }
            }
        }

        protected virtual EndpointConfiguration CreateEndpointConfiguration(Settings settings)
        {
            var busConfiguration = new EndpointConfiguration(settings.ServiceName);
            var assemblyScanner = busConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            return busConfiguration;
        }
    }
}