namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using Microsoft.Extensions.DependencyInjection;
    using NLog;
    using NServiceBus;
    using ServiceControl.Audit.Persistence;
    using Settings;

    class ImportFailedAuditsCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            var settings = new Settings(args.ServiceName)
            {
                IngestAuditMessages = false
            };

            var persistenceSettings = new PersistenceSettings(settings.AuditRetentionPeriod);

            var busConfiguration = new EndpointConfiguration(settings.ServiceName);

            using (var tokenSource = new CancellationTokenSource())
            {
                var loggingSettings = new LoggingSettings(settings.ServiceName, LogLevel.Info, LogLevel.Info);
                var bootstrapper = new Bootstrapper(
                    ctx => { tokenSource.Cancel(); },
                    settings,
                    busConfiguration,
                    loggingSettings,
                    persistenceSettings);

                var host = bootstrapper.HostBuilder.Build();

                await host.StartAsync(tokenSource.Token).ConfigureAwait(false);

                var importer = host.Services.GetRequiredService<ImportFailedAudits>();

                Console.CancelKeyPress += (sender, eventArgs) => { tokenSource.Cancel(); };

                try
                {
                    await importer.Run(tokenSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // no op
                }
                finally
                {
                    await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
    }
}