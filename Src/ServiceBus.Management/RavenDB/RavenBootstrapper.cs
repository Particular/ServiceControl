namespace ServiceBus.Management.RavenDB
{
    using System;
    using System.IO;
    using NServiceBus;
    using Raven.Client;
    using Raven.Client.Embedded;

    public class RavenBootstrapper : INeedInitialization
    {
        public void Init()
        {
            const int port = 8082;
            var documentStore = new EmbeddableDocumentStore
                {
                    DataDirectory = RetrieveDbPath(),
                    UseEmbeddedHttpServer = true,
                    ResourceManagerId = new Guid("{1AD6E17D-74FF-445B-925D-F22C4A82B30A}"),
                };

            documentStore.Configuration.Port = port;

            documentStore.Initialize();

            Configure.Instance.Configurer.RegisterSingleton<IDocumentStore>(documentStore);

            Configure.Instance.RavenPersistence();
        }


        static string RetrieveDbPath()
        {
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NServiceBus", "Monitor");

            Directory.CreateDirectory(dbPath);

            return dbPath;
        }
    }
}