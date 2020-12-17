namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using Hosting;
    using Raven.Client.Embedded;
    using RavenDB;

    class MaintenanceBootstrapper
    {
        public void Run(HostArguments args)
        {
            var settings = new Settings.Settings(args.ServiceName);
            var documentStore = new EmbeddableDocumentStore();

            new RavenBootstrapper().StartRaven(documentStore, settings, true);

            if (args.Portable)
            {
                Console.Out.WriteLine("RavenDB is now accepting requests on {0}", settings.StorageUrl);
                Console.Out.WriteLine("RavenDB Maintenance Mode - Press Enter to exit");
                while (Console.ReadKey().Key != ConsoleKey.Enter)
                {
                }

                documentStore.Dispose();

                return;
            }
            else
            {
                using (var service = new MaintenanceHost(settings, documentStore))
                {
                    service.Run();
                }
            }
        }
    }
}