namespace Particular.ServiceControl
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::ServiceControl.Infrastructure.RavenDB;
    using Hosting;
    using NServiceBus.Logging;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;

    class MaintenanceBootstrapper
    {
        public async Task Run(HostArguments args)
        {
            var settings = new Settings(args.ServiceName);
            var documentStore = new EmbeddableDocumentStore();

            new RavenBootstrapper().StartRaven(documentStore, settings, true);

            if (args.RunAsWindowsService)
            {
                using (var service = new MaintenanceHost(settings, documentStore))
                {
                    service.Run();
                }
            }
            else
            {
                await Console.Out.WriteLineAsync($"RavenDB is now accepting requests on {settings.StorageUrl}").ConfigureAwait(false);
                await Console.Out.WriteLineAsync("RavenDB Maintenance Mode - Press CTRL+C to exit").ConfigureAwait(false);

                var closing = new AutoResetEvent(false);

                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    closing.Set();
                };

                closing.WaitOne();

                await Console.Out.WriteLineAsync("Disposing RavenDB document store (this might take a while)...").ConfigureAwait(false);
                documentStore.Dispose();
                await Console.Out.WriteLineAsync("Done!").ConfigureAwait(false);
            }
        }
    }
}