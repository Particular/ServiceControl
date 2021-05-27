namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using Hosting;
    using Raven.Client.Embedded;
    using RavenDB;

    static class MaintenanceBootstrapper
    {
        public static async Task Run(HostArguments args)
        {
            var settings = new Settings.Settings(args.ServiceName);
            using (var documentStore = new EmbeddableDocumentStore())
            {
                RavenBootstrapper.ConfigureAndStart(documentStore, settings, true);

                if (args.RunAsWindowsService)
                {
                    using (var service = new MaintenanceHost(settings))
                    {
                        service.Run();
                    }
                }
                else
                {
                    await Console.Out.WriteLineAsync($"RavenDB is now accepting requests on {settings.StorageUrl}")
                        .ConfigureAwait(false);
                    await Console.Out.WriteLineAsync("RavenDB Maintenance Mode - Press CTRL+C to exit")
                        .ConfigureAwait(false);

                    var taskCompletionSource =
                        new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                    Console.CancelKeyPress += (sender, eventArgs) =>
                    {
                        eventArgs.Cancel = true;
                        taskCompletionSource.TrySetResult(true);
                    };

                    await taskCompletionSource.Task.ConfigureAwait(false);

                    await Console.Out.WriteLineAsync("Disposing RavenDB document store (this might take a while)...")
                        .ConfigureAwait(false);
                }
                await Console.Out.WriteLineAsync("Done!").ConfigureAwait(false);
            }
        }
    }
}