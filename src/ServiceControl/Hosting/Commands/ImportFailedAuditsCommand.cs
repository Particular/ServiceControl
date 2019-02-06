﻿namespace Particular.ServiceControl.Commands
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
        public override void Execute(HostArguments args)
        {
            RunAndWait(args).GetAwaiter().GetResult();
        }


        async Task RunAndWait(HostArguments args)
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
            var tokenSource = new CancellationTokenSource();

            var loggingSettings = new LoggingSettings(settings.ServiceName, LogLevel.Info, LogLevel.Info);
            using (var bootstrapper = new Bootstrapper(ctx => { tokenSource.Cancel(); }, settings, busConfiguration, loggingSettings))
            {
                var importer = bootstrapper.Start().GetAwaiter().GetResult().ImportFailedAudits;

                Console.CancelKeyPress += (sender, eventArgs) => { tokenSource.Cancel(); };

                var importTask = importer.Run(tokenSource);

                await importTask.ConfigureAwait(false);

                await bootstrapper.Stop().ConfigureAwait(false);
            }
        }
    }
}