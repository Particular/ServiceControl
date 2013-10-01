namespace ServiceBus.Management.Infrastructure.RavenDB
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Indexing;
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

            NonAdminHttp.EnsureCanListenToWhenInNonAdminContext(Settings.Port);

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

            // TODO: Move the index outta here to a separate task
            // Create the index
            documentStore.DatabaseCommands.PutIndex("MessageFailures",
                   new IndexDefinition
                   {
                       Map = @"from message in docs 
                                where message.Status == ""Failed""
                                select new 
                                { 
                                    message.ReceivingEndpoint.Name, 
                                    message.ReceivingEndpoint.Machine, 
                                    message.MessageType,
                                    message.FailureDetails.Exception.ExceptionType,
                                    message.FailureDetails.Exception.Message,
                                    message.TimeSent
                                }"
                   }, true);


            // Create the facets for MessageFailures to facilitate easy searching.
            var facets = new List<Facet>
            {
                new Facet {Name = "Name", DisplayName="Endpoints"},
                new Facet {Name = "Machine", DisplayName = "Machines"},
                new Facet {Name = "MessageType", DisplayName = "Message Types"},
                //new Facet() {Name = "Custom Tags"}
            };

            using (var s = documentStore.OpenSession())
            {
                s.Store(new FacetSetup { Id = "facets/messageFailureFacets", Facets = facets });
                s.SaveChanges();
            }

            Configure.Instance.Configurer.RegisterSingleton<IDocumentStore>(documentStore);
            Configure.Component<RavenUnitOfWork>(DependencyLifecycle.InstancePerUnitOfWork);
            Configure.Instance.RavenPersistenceWithStore(documentStore);
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenBootstrapper));
    }
}