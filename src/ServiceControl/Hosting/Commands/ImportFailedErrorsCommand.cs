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
    using ServiceBus.Management.Infrastructure.Settings;

    class ImportFailedErrorsCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            var settings = new Settings(args.ServiceName)
            {
                IngestErrorMessages = false,
                RunRetryProcessor = false,
                DisableHealthChecks = true
            };

            var busConfiguration = new EndpointConfiguration(settings.ServiceName);
            var assemblyScanner = busConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");
            var tokenSource = new CancellationTokenSource();

            var loggingSettings = new LoggingSettings(settings.ServiceName, LogLevel.Info, LogLevel.Info);
            var bootstrapper = new Bootstrapper(settings, busConfiguration, loggingSettings);
            var host = bootstrapper.HostBuilder.Build();
            await host.StartAsync(CancellationToken.None).ConfigureAwait(false);

            var errorIngestion = host.Services.GetRequiredService<ErrorIngestionComponent>();

            Console.CancelKeyPress += (sender, eventArgs) => { tokenSource.Cancel(); };

            try
            {
                await errorIngestion.ImportFailedErrors(tokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // no-op
            }
            finally
            {
                await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}