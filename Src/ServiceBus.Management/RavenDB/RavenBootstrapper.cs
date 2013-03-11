namespace ServiceBus.Management.RavenDB
{
    using System;
    using System.IO;
    using NServiceBus;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;

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
                    EnlistInDistributedTransactions = false
                };
            
            documentStore.Configuration.Port = port;

            documentStore.Initialize();

            IndexCreation.CreateIndexes(typeof(RavenBootstrapper).Assembly, documentStore);

            Configure.Instance.Configurer.RegisterSingleton<IDocumentStore>(documentStore);
            Configure.Instance.RavenPersistence(documentStore);
        }


        static string RetrieveDbPath()
        {
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceBus.Management");

            Directory.CreateDirectory(dbPath);

            return dbPath;
        }
    }
}