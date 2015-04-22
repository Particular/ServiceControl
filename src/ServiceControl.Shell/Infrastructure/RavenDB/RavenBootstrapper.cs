namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.ComponentModel.Composition.Hosting;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Pipeline;
    using NServiceBus.RavenDB;
    using Particular.Backend.Debugging.RavenDB.Expiration;
    using Particular.ServiceControl.Licensing;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using ServiceBus.Management.Infrastructure.Settings;
    using INeedInitialization = NServiceBus.INeedInitialization;

    public class RavenBootstrapper : INeedInitialization
    {
        public static string ReadLicense()
        {
            using (var resourceStream = typeof(RavenBootstrapper).Assembly.GetManifestResourceStream("ServiceControl.Shell.Infrastructure.RavenDB.RavenLicense.xml"))
            using (var reader = new StreamReader(resourceStream))
            {
                return reader.ReadToEnd();
            }
        }

        public void Init()
        {
            Directory.CreateDirectory(Settings.DbPath);

            var documentStore = new EmbeddableDocumentStore
            {
                DataDirectory = Settings.DbPath,
                UseEmbeddedHttpServer = true, // Settings.MaintenanceMode || Settings.ExposeRavenDB,
                EnlistInDistributedTransactions = false,
            };

            var serviceControlAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => x.GetName().Name.StartsWith("ServiceControl") || x.GetName().Name.StartsWith("Particular.")).ToArray();

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

            foreach (var serviceControlAssembly in serviceControlAssemblies)
            {
                documentStore.Configuration.Catalog.Catalogs.Add(new AssemblyCatalog(serviceControlAssembly));
            }

            if (!Settings.MaintenanceMode)
            {
                // Without this line the  Raven Bundle won't execute as it won't be found
                // ReSharper disable once UnusedVariable
                var expiredDocumentCleanerType = typeof(ExpiredDocumentsCleaner);

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

            //if (Settings.CreateIndexSync)
            //{
                foreach (var serviceControlAssembly in serviceControlAssemblies)
                {
                    IndexCreation.CreateIndexes(serviceControlAssembly, documentStore);
                }
            //}
            //else
            //{
            //    foreach (var serviceControlAssembly in serviceControlAssemblies)
            //    {
            //        IndexCreation.CreateIndexesAsync(serviceControlAssembly, documentStore)
            //            .ContinueWith(c =>
            //            {
            //                if (c.IsFaulted)
            //                {
            //                    Logger.Error("Index creation failed", c.Exception);
            //                }
            //            }, TaskContinuationOptions.OnlyOnFaulted);
            //    }
            //}

            // standard ravendb index. created by studio on first use. used by sagahistory migrations
            new RavenDocumentsByEntityName()
                .ExecuteAsync(documentStore.AsyncDatabaseCommands,documentStore.Conventions);


            Configure.Instance.Configurer.RegisterSingleton<IDocumentStore>(documentStore);
            Configure.Component(builder =>
            {
#pragma warning disable 618
                var context = builder.Build<PipelineExecutor>().CurrentContext;
#pragma warning restore 618

                IDocumentSession session;

                if (context.TryGet(out session))
                {
                    return session;
                }

                throw new InvalidOperationException("No session available");
            }, DependencyLifecycle.InstancePerCall);

            Configure.Instance.RavenDBStorageWithSelfManagedSession(documentStore, false,
                () => Configure.Instance.Builder.Build<IDocumentSession>())
                .UseRavenDBSagaStorage()
                .UseRavenDBSubscriptionStorage()
                .UseRavenDBTimeoutStorage();
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenBootstrapper));
    }
}
