namespace Particular.ServiceControl
{
    using System;
    using global::ServiceControl.Infrastructure.RavenDB;
    using Particular.ServiceControl.Hosting;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;

    public class MaintenanceBootstrapper
    {
        public void Run(HostArguments args)
        {
            var settings = new Settings(args.ServiceName);
            var documentStore = new EmbeddableDocumentStore();

            RavenBootstrapper.StartRaven(documentStore, settings, true);

            if (Environment.UserInteractive)
            {
                Console.Out.WriteLine("RavenDB is now accepting requests on {0}", settings.StorageUrl);
                Console.Out.WriteLine("RavenDB Maintenance Mode - Press Enter to exit");
                while (Console.ReadKey().Key != ConsoleKey.Enter)
                {
                }
                documentStore.Dispose();

                return;
            }

            using (var service = new MaintenanceHost(settings, documentStore))
            {
                service.Run();
            }
        }
    }
}