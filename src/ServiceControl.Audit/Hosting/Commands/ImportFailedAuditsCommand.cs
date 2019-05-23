namespace Particular.ServiceControl.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Hosting;
    using NLog;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

    class ImportFailedAuditsCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            var settings = new Settings(args.ServiceName)
            {
                IngestAuditMessages = false,
                IngestErrorMessages = false,
                RunRetryProcessor = false
            };

            var busConfiguration = new EndpointConfiguration(settings.ServiceName);
            var assemblyScanner = busConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            using (var tokenSource = new CancellationTokenSource())
            {
                var loggingSettings = new LoggingSettings(settings.ServiceName, LogLevel.Info, LogLevel.Info);
                var bootstrapper = new Bootstrapper(ctx => { tokenSource.Cancel(); }, settings, busConfiguration, loggingSettings);
                var busInstance = await bootstrapper.Start().ConfigureAwait(false);
                var importer = busInstance.ImportFailedAudits;

                Console.CancelKeyPress += (sender, eventArgs) => { tokenSource.Cancel(); };

                try
                {
                    await importer.Run(tokenSource).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // no op
                }
                finally
                {
                    await bootstrapper.Stop().ConfigureAwait(false);
                }
            }
        }
    }
}