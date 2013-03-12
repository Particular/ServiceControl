namespace ServiceBus.Management.RavenDB
{
    using System;
    using System.IO;
    using NServiceBus;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using Raven.Database.Server;

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

            //TODO: We need to do more robust check here, but at the same time I wonder if we even need to expose raven via http?
            NonAdminHttp.EnsureCanListenToWhenInNonAdminContext(port);

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