namespace ServiceControl.Infrastructure.RavenDB
{
    using System.IO;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.RavenDB;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using ServiceBus.Management.Infrastructure.RavenDB;
    using ServiceBus.Management.Infrastructure.Settings;

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
            
            documentStore.Configuration.Port = Settings.Port;
            documentStore.Configuration.HostName = (Settings.Hostname == "*" || Settings.Hostname == "+") ? "localhost" : Settings.Hostname;
            documentStore.Configuration.CompiledIndexCacheDirectory = Settings.DbPath;
            documentStore.Configuration.VirtualDirectory = Settings.VirtualDirectory + "/storage";

            documentStore.Initialize();

            Logger.Info("Index creation started");

            if (Settings.CreateIndexSync)
            {
                IndexCreation.CreateIndexes(typeof(RavenBootstrapper).Assembly, documentStore);    
            }
            else
            {
                IndexCreation.CreateIndexesAsync(typeof(RavenBootstrapper).Assembly, documentStore)
                    .ContinueWith(c =>
                    {
                        if (c.IsFaulted)
                        {
                            Logger.Error("Index creation failed", c.Exception);
                        }
                    });                
            }

            Configure.Instance.Configurer.RegisterSingleton<IDocumentStore>(documentStore);
            Configure.Component<RavenUnitOfWork>(DependencyLifecycle.InstancePerUnitOfWork);


            Configure.Instance.RavenDBPersistence(documentStore, false);
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenBootstrapper));
    }
}
