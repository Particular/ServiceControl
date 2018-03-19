namespace Particular.ServiceControl.Commands
{
    using System;
    using System.Threading;
    using global::ServiceControl.Operations;
    using Hosting;
    using NLog;
    using NServiceBus;
    using NServiceBus.Unicast;
    using ServiceBus.Management.Infrastructure.Settings;

    internal class ImportFailedAuditsCommand : AbstractCommand
    {
        public override void Execute(HostArguments args)
        {
            RunAndWait(args);
        }


        void RunAndWait(HostArguments args)
        {
            var busConfiguration = new BusConfiguration();
            busConfiguration.AssembliesToScan(AllAssemblies.Except("ServiceControl.Plugin"));
            var settings = new Settings
            {
                IngestAuditMessages = false,
                IngestErrorMessages = false
            };

            var tokenSource = new CancellationTokenSource();

            var loggingSettings = new LoggingSettings(settings.ServiceName, LogLevel.Info, LogLevel.Info);
            var bootstrapper = new Bootstrapper(() => { tokenSource.Cancel();}, settings, busConfiguration, loggingSettings);
            var bus = (UnicastBus)bootstrapper.Start().Bus;

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                tokenSource.Cancel();
            };

            var importTask = bus.Builder.Build<ImportFailedAudits>().Run(tokenSource);

            Console.WriteLine("Press Ctrl+C to exit");
            importTask.GetAwaiter().GetResult();
        }
    }
}