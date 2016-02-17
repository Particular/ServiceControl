namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.ComponentModel.Composition.Hosting;
    using System.IO;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Logging;
    using NServiceBus.Persistence;
    using NServiceBus.Pipeline;
    using Particular.ServiceControl.Licensing;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.EndpointControl;
    using INeedInitialization = NServiceBus.INeedInitialization;

    public class RavenBootstrapper : INeedInitialization
    {
        public static string ReadLicense()
        {
            using (var resourceStream = typeof(RavenBootstrapper).Assembly.GetManifestResourceStream("ServiceControl.Infrastructure.RavenDB.RavenLicense.xml"))
            using (var reader = new StreamReader(resourceStream))
            {
                return reader.ReadToEnd();
            }
        }

        public void Customize(BusConfiguration configuration)
        {
            var documentStore = configuration.GetSettings().Get<EmbeddableDocumentStore>("ServiceControl.EmbeddableDocumentStore");

            Directory.CreateDirectory(Settings.DbPath);

            documentStore.DataDirectory = Settings.DbPath;
            documentStore.UseEmbeddedHttpServer = Settings.MaintenanceMode || Settings.ExposeRavenDB;
            documentStore.EnlistInDistributedTransactions = false;

            var localRavenLicense = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RavenLicense.xml");
            if (File.Exists(localRavenLicense))
            {
                Logger.InfoFormat("Loading RavenDB license found from {0}", localRavenLicense);
                documentStore.Configuration.Settings["Raven/License"] = NonLockingFileReader.ReadAllTextWithoutLocking(localRavenLicense);
            }
            else
            {
                Logger.InfoFormat("Loading Embedded RavenDB license");
                documentStore.Configuration.Settings["Raven/License"] = ReadLicense();
            }

            documentStore.Configuration.Catalog.Catalogs.Add(new AssemblyCatalog(GetType().Assembly));
            
            if (!Settings.MaintenanceMode) {
                documentStore.Configuration.Settings.Add("Raven/ActiveBundles", "CustomDocumentExpiration");
            }

            documentStore.Configuration.Port = Settings.Port;
            documentStore.Configuration.HostName = (Settings.Hostname == "*" || Settings.Hostname == "+")
                ? "localhost"
                : Settings.Hostname;
            documentStore.Configuration.CompiledIndexCacheDirectory = Settings.DbPath;
            documentStore.Configuration.VirtualDirectory = Settings.VirtualDirectory + "/storage";
            documentStore.Conventions.SaveEnumsAsIntegers = true;
            documentStore.Initialize();

            Logger.Info("Index creation started");
            
            //Create this index synchronously as we are using it straight away
            //Should be quick as number of endpoints will always be a small number
            documentStore.ExecuteIndex(new KnownEndpointIndex());
            
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

            PurgeKnownEndpointsWithTemporaryIdsThatAreDuplicate(documentStore);

            configuration.RegisterComponents(c => 
                 c.ConfigureComponent(builder =>
                 {
                     var context = builder.Build<PipelineExecutor>().CurrentContext;

                     IDocumentSession session;

                     if (context.TryGet(out session))
                     {
                         return session;
                     }

                     throw new InvalidOperationException("No session available");
                 }, DependencyLifecycle.InstancePerCall));

            configuration.UsePersistence<RavenDBPersistence>()
                         .SetDefaultDocumentStore(documentStore);

            configuration.Pipeline.Register<RavenRegisterStep>();
        }

        static void PurgeKnownEndpointsWithTemporaryIdsThatAreDuplicate(IDocumentStore documentStore)
        {
            using (var session = documentStore.OpenSession())
            {
                var endpoints = session.Query<KnownEndpoint, KnownEndpointIndex>().ToList();

                foreach (var knownEndpoints in endpoints.GroupBy(e => e.EndpointDetails.Host + e.EndpointDetails.Name))
                {
                    var fixedIdsCount = knownEndpoints.Count(e => !e.HasTemporaryId);

                    //If we have knowEndpoints with non temp ids, we should delete all temp ids ones.
                    if (fixedIdsCount > 0)
                    {
                        knownEndpoints.Where(e => e.HasTemporaryId).ForEach(k => { documentStore.DatabaseCommands.Delete(documentStore.Conventions.DefaultFindFullDocumentKeyFromNonStringIdentifier(k.Id, typeof(KnownEndpoint), false), null); });
                    }
                }
            }
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenBootstrapper));
    }
}
