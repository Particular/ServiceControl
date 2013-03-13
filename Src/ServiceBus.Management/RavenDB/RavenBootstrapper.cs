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
            Directory.CreateDirectory(Settings.DbPath);

            var documentStore = new EmbeddableDocumentStore
                {
                    DataDirectory = Settings.DbPath,
                    UseEmbeddedHttpServer = true,
                    EnlistInDistributedTransactions = false
                };

            //TODO: We need to do more robust check here
            NonAdminHttp.EnsureCanListenToWhenInNonAdminContext(Settings.Port);

            documentStore.Configuration.Port = Settings.Port;
            documentStore.Configuration.HostName = Settings.Hostname;

            documentStore.Configuration.VirtualDirectory = Settings.VirtualDirectory + "/storage";

            documentStore.Initialize();

            IndexCreation.CreateIndexes(typeof(RavenBootstrapper).Assembly, documentStore);

            Configure.Instance.Configurer.RegisterSingleton<IDocumentStore>(documentStore);
            Configure.Instance.RavenPersistence(documentStore);
        }
    }
}