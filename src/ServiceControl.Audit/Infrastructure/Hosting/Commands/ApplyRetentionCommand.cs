namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Persistence;
    using ServiceControl.Infrastructure;
    using Settings;

    /// <summary>
    /// Applies the configured audit retention period to the stored messages. Run it after a change of
    /// <c>ServiceControl.Audit/AuditRetentionPeriod</c>: ingestion sets the expiry of each message when it is
    /// stored, so the stored messages keep the old retention period until this command rewrites their expiry.
    /// </summary>
    class ApplyRetentionCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings, CancellationToken cancellationToken = default)
        {
            var logger = LoggerUtil.CreateStaticLogger<ApplyRetentionCommand>();

            var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration(settings);
            var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings(settings);

            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.Services.AddPersistence(persistenceSettings, persistenceConfiguration);

            using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                tokenSource.Cancel();
            };

            using var host = hostBuilder.Build();
            await host.StartAsync(tokenSource.Token);

            try
            {
                var applier = host.Services.GetService<IAuditRetentionApplier>();

                if (applier == null)
                {
                    logger.LogInformation("The {PersistenceType} persister applies the retention period when it removes expired messages, so the stored messages need no update", settings.PersistenceType);
                    return;
                }

                logger.LogInformation("Applying the audit retention period of {RetentionPeriod} to the stored messages", settings.AuditRetentionPeriod);

                var progress = new Progress<long>(count => logger.LogInformation("{Count:N0} documents updated so far", count));
                var updated = await applier.ApplyRetention(settings.AuditRetentionPeriod, progress, tokenSource.Token);

                logger.LogInformation("Done. Updated the expiry of {Count:N0} documents", updated);
            }
            catch (OperationCanceledException e) when (tokenSource.Token.IsCancellationRequested)
            {
                logger.LogInformation(e, "Cancelled");
            }
            finally
            {
                await host.StopAsync(CancellationToken.None);
            }
        }
    }
}
