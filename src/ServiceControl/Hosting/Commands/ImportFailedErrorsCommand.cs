﻿namespace ServiceControl.Hosting.Commands
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
        public override async Task Execute(HostArguments args, Settings settings)
        {
            settings.IngestErrorMessages = false;
            settings.RunRetryProcessor = false;
            settings.DisableHealthChecks = true;

            var busConfiguration = new EndpointConfiguration(settings.ServiceName);
            var assemblyScanner = busConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            var loggingSettings = new LoggingSettings(settings.ServiceName, LogLevel.Info, LogLevel.Info);
            var bootstrapper = new Bootstrapper(settings, busConfiguration, loggingSettings);
            var host = bootstrapper.HostBuilder.Build();
            await host.StartAsync(CancellationToken.None);

            var importFailedErrors = host.Services.GetRequiredService<ImportFailedErrors>();

            var tokenSource = new CancellationTokenSource();

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
            }
        }
    }
}