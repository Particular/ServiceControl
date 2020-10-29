using ServiceControl.Infrastructure.RavenDB;

namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NLog;
    using NServiceBus;
    using Settings;

    class ImportFailedAuditsCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            var settings = new Settings(args.ServiceName)
            {
                IngestAuditMessages = false
            };

            var busConfiguration = new EndpointConfiguration(settings.ServiceName);
            var assemblyScanner = busConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            using (var tokenSource = new CancellationTokenSource())
            {
                var loggingSettings = new LoggingSettings(settings.ServiceName, LogLevel.Info, LogLevel.Info);
                var embeddedDatabase = EmbeddedDatabase.Start(settings.DbPath, loggingSettings.LogPath, settings.RavenDBNetCoreRuntimeVersion, settings.ExpirationProcessTimerInSeconds, settings.DatabaseMaintenanceUrl);
                var bootstrapper = new Bootstrapper(ctx => { tokenSource.Cancel(); }, settings, busConfiguration, loggingSettings, embeddedDatabase);
                var busInstance = await bootstrapper.Start().ConfigureAwait(false);
                var importer = busInstance.AuditIngestion;

                Console.CancelKeyPress += (sender, eventArgs) => { tokenSource.Cancel(); };

                try
                {
                    await importer.ImportFailedAudits(tokenSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // no op
                }
                finally
                {
                    await bootstrapper.Stop().ConfigureAwait(false);
                    embeddedDatabase.Dispose();
                }
            }
        }
    }
}