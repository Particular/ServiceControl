using ServiceControl.Infrastructure.RavenDB;

namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NLog;
    using NServiceBus;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;

    class ImportFailedErrorsCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            var settings = new Settings(args.ServiceName)
            {
                IngestErrorMessages = false,
                RunRetryProcessor = false
            };

            var busConfiguration = new EndpointConfiguration(settings.ServiceName);
            var assemblyScanner = busConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");
            var tokenSource = new CancellationTokenSource();

            var loggingSettings = new LoggingSettings(settings.ServiceName, LogLevel.Info);
            var embeddedDatabase = EmbeddedDatabase.Start(settings.DbPath, loggingSettings.LogPath, settings.ExpirationProcessTimerInSeconds, settings.DatabaseMaintenanceUrl);
            var bootstrapper = new Bootstrapper(settings, busConfiguration, loggingSettings, embeddedDatabase);
            var instance = await bootstrapper.Start().ConfigureAwait(false);
            var errorIngestion = instance.ErrorIngestion;

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
                await bootstrapper.Stop().ConfigureAwait(false);
                embeddedDatabase.Dispose();
            }
        }
    }
}