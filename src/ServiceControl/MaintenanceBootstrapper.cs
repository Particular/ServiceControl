namespace Particular.ServiceControl
{
    using System;
    using Hosting;
    using Raven.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;

    class MaintenanceBootstrapper
    {
        public void Run(HostArguments args)
        {
            var settings = new Settings(args.ServiceName);
            EmbeddedServer.Instance.StartServer();
            var documentStore = EmbeddedServer.Instance.GetDocumentStore("servicecontrol");

            //TODO:RAVEN5 RavenBootstraper should probably be gone
            //new RavenBootstrapper().StartRaven(documentStore, settings, true);

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