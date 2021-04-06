namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Hosting;
    using Raven.Client.Embedded;
    using RavenDB;

    class MaintenanceBootstrapper
    {
        public async Task Run(HostArguments args)
        {
            var settings = new Settings.Settings(args.ServiceName);
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

                using (var cts = new CancellationTokenSource())
                {
                    Console.CancelKeyPress += (sender, eventArgs) => cts.Cancel();
                    await Task.Delay(-1, cts.Token).ConfigureAwait(false);
                }
                documentStore.Dispose();
            }
        }
    }
}