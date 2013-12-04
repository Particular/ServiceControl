namespace ServiceBus.Management.Infrastructure.RavenDB
{
    using System.Diagnostics;
    using System.IO;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using Raven.Database.Server;
    using Settings;

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
            documentStore.Configuration.HostName = Settings.Hostname;
            documentStore.Configuration.CompiledIndexCacheDirectory = Settings.DbPath;
            documentStore.Configuration.VirtualDirectory = Settings.VirtualDirectory + "/storage";

            documentStore.Initialize();

            var sw = new Stopwatch();

            sw.Start();
            Logger.Info("Index creation started");

            IndexCreation.CreateIndexesAsync(typeof(RavenBootstrapper).Assembly, documentStore)
                .ContinueWith(c =>
                {
                    sw.Stop();
                    if (c.IsFaulted)
                    {
                        Logger.Error("Index creation failed", c.Exception);
                    }
                    else
                    {
                        Logger.InfoFormat("Index creation completed, total time: {0}", sw.Elapsed);
                    }
                });

            Configure.Instance.Configurer.RegisterSingleton<IDocumentStore>(documentStore);
            Configure.Component<RavenUnitOfWork>(DependencyLifecycle.InstancePerUnitOfWork);
            Configure.Instance.RavenPersistenceWithStore(documentStore);
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenBootstrapper));
    }
}