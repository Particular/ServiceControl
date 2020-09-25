namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using Hosting;
    using Raven.Embedded;
    using Settings;

    class MaintenanceBootstrapper
    {
        public void Run(HostArguments args)
        {
            var settings = new Settings.Settings(args.ServiceName);
            var loggingSettings = new LoggingSettings(args.ServiceName);

            // TODO: RAVEN5 - This used to be an EmbeddableServer
            EmbeddedDatabase.Start(settings, loggingSettings);
            var documentStore = EmbeddedServer.Instance.GetDocumentStore("audit");
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