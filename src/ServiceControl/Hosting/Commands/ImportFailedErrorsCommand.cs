namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NLog;
    using NServiceBus;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Commands;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class ImportFailedErrorsCommand : AbstractCommand
    {
        public override void Execute(HostArguments args)
        {
            RunAndWait(args.ServiceName).GetAwaiter().GetResult();
            //TODO see ravendb disposal message after command completed
        }

        async Task RunAndWait(string serviceName)
        {
            var settings = new Settings(serviceName)
            {
                IngestAuditMessages = false,
                IngestErrorMessages = false
            };

            var busConfiguration = new EndpointConfiguration(settings.ServiceName);
            var assemblyScanner = busConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");
            var tokenSource = new CancellationTokenSource();

            var loggingSettings = new LoggingSettings(settings.ServiceName, LogLevel.Info, LogLevel.Info);
            var bootstrapper = new Bootstrapper(ctx => { tokenSource.Cancel(); }, settings, busConfiguration, loggingSettings);
            var instance = await bootstrapper.Start().ConfigureAwait(false);
            var importer = instance.ImportFailedErrors;

            Console.CancelKeyPress += (sender, eventArgs) => { tokenSource.Cancel(); };

            var importTask = importer.Run(tokenSource);

            Console.WriteLine("Press Ctrl+C to exit");
            await importTask.ConfigureAwait(false);
        }
    }
}