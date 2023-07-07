namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using Microsoft.Extensions.DependencyInjection;
    using NLog;
    using NServiceBus;
    using Settings;

    class ImportFailedAuditsCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            settings.IngestAuditMessages = false;

            var busConfiguration = new EndpointConfiguration(settings.ServiceName);

            using (var tokenSource = new CancellationTokenSource())
            {
                var loggingSettings = new LoggingSettings(settings.ServiceName, LogLevel.Info, LogLevel.Info);
                var bootstrapper = new Bootstrapper(
                    ctx => { tokenSource.Cancel(); },
                    settings,
                    busConfiguration,
                    loggingSettings);

                var host = bootstrapper.HostBuilder.Build();

                await host.StartAsync(tokenSource.Token);

                var importer = host.Services.GetRequiredService<ImportFailedAudits>();

                Console.CancelKeyPress += (sender, eventArgs) => { tokenSource.Cancel(); };

                try
                {
                    await importer.Run(tokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // no op
                }
                finally
                {
                    await host.StopAsync(CancellationToken.None);
                }
            }
        }
    }
}