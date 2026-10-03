namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using Operations;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;
    using ServiceControl.Migration;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.DataMigration;

    class ImportFailedErrorsCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings, CancellationToken cancellationToken = default)
        {
            using var app = BuildHost(settings);
            await app.StartAsync(cancellationToken);

            var importFailedErrors = app.Services.GetRequiredService<ImportFailedErrors>();

            using var tokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, _) => tokenSource.Cancel();

            try
            {
                await importFailedErrors.Run(tokenSource.Token);
            }
            catch (OperationCanceledException e) when (tokenSource.Token.IsCancellationRequested)
            {
                LoggerUtil.CreateStaticLogger<ImportFailedErrorsCommand>().LogInformation(e, "Cancelled");
            }
            finally
            {
                await app.StopAsync(CancellationToken.None);
            }
        }

        internal static IHost BuildHost(Settings settings)
        {
            settings.IngestErrorMessages = false;
            settings.RunRetryProcessor = false;
            settings.DisableHealthChecks = true;

            var endpointConfiguration = new EndpointConfiguration(settings.InstanceName);
            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.Disable = true;

            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.AddServiceControl(settings, endpointConfiguration, new RecoverabilityComponent());

            // Importing writes failed messages into the database, so on SQL it waits for an unfinished copy as an ingestion-only worker does.
            if (PersistenceFactory.SqlPersistenceNames.Contains(PersistenceManifestLibrary.Find(settings.PersistenceType)?.Name, StringComparer.OrdinalIgnoreCase))
            {
                hostBuilder.Services.AddHostedService(provider =>
                    new FinishedCopyBeforeAnIngestionNodeOpens(
                        provider.GetRequiredService<IMigrationCheckpointStore>(),
                        provider.GetRequiredService<IMigrationTargetReadiness>(),
                        settings,
                        "--import-failed-errors",
                        provider.GetRequiredService<ILogger<FinishedCopyBeforeAnIngestionNodeOpens>>()));
            }

            return hostBuilder.Build();
        }
    }
}