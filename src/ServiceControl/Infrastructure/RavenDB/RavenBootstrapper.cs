namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using Audit.Monitoring;
    using Monitoring;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using Particular.Licensing;
    using Raven.Client.Documents;
    using Raven.Embedded;
    using Raven.Client.Documents.Indexes;
    using ServiceBus.Management.Infrastructure.Settings;
    using Subscriptions;

    class RavenBootstrapper : INeedInitialization
    {
        public static Settings Settings { get; set; }

        public bool RunCleanup { get; set; }

        public void Customize(EndpointConfiguration configuration)
        {
            var documentStore = configuration.GetSettings().Get<EmbeddedServer>();
            var settings = configuration.GetSettings().Get<Settings>("ServiceControl.Settings");

            Settings = settings;

            StartRaven(documentStore, settings, false);

            configuration.UsePersistence<CachedRavenDBPersistence, StorageType.Subscriptions>();
        }

        public static string ReadLicense()
        {
            using (var resourceStream = typeof(RavenBootstrapper).Assembly.GetManifestResourceStream("ServiceControl.Infrastructure.RavenDB.RavenLicense.xml"))
            using (var reader = new StreamReader(resourceStream))
            {
                return reader.ReadToEnd();
            }
        }

        public void StartRaven(EmbeddedServer documentStore, Settings settings, bool maintenanceMode)
        {
            Settings = settings;

            Directory.CreateDirectory(settings.DbPath);

            documentStore.Listeners.RegisterListener(new SubscriptionsLegacyAddressConverter());

            if (settings.RunInMemory)
            {
                documentStore.RunInMemory = true;
            }
            else
            {
                documentStore.DataDirectory = settings.DbPath;
                documentStore.Configuration.CompiledIndexCacheDirectory = settings.DbPath;
            }

            documentStore.UseEmbeddedHttpServer = maintenanceMode || settings.ExposeRavenDB;
            documentStore.EnlistInDistributedTransactions = false;

            var localRavenLicense = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RavenLicense.xml");
            if (File.Exists(localRavenLicense))
            {
                Logger.InfoFormat("Loading RavenDB license found from {0}", localRavenLicense);
                documentStore.Configuration.Settings["Raven/License"] = NonBlockingReader.ReadAllTextWithoutLocking(localRavenLicense);
            }
            else
            {
                Logger.InfoFormat("Loading Embedded RavenDB license");
                documentStore.Configuration.Settings["Raven/License"] = ReadLicense();
            }

            //This is affects only remote access to the database in maintenace mode and enables access without authentication
            documentStore.Configuration.Settings["Raven/AnonymousAccess"] = "Admin";
            documentStore.Configuration.Settings["Raven/Licensing/AllowAdminAnonymousAccessForCommercialUse"] = "true";

            if (Settings.RunCleanupBundle)
            {
                documentStore.Configuration.Settings.Add("Raven/ActiveBundles", "CustomDocumentExpiration");
            }

            documentStore.Configuration.DisableClusterDiscovery = true;
            documentStore.Configuration.ResetIndexOnUncleanShutdown = true;
            documentStore.Configuration.Port = settings.DatabaseMaintenancePort;
            documentStore.Configuration.HostName = settings.Hostname == "*" || settings.Hostname == "+"
                ? "localhost"
                : settings.Hostname;
            documentStore.Conventions.SaveEnumsAsIntegers = true;
            documentStore.Conventions.CustomizeJsonSerializer = serializer => serializer.Binder = MigratedTypeAwareBinder;

            documentStore.Configuration.Catalog.Catalogs.Add(new AssemblyCatalog(GetType().Assembly));

            documentStore.Initialize();

            Logger.Info("Index creation started");

            IndexCreation.CreateIndexes(typeof(RavenBootstrapper).Assembly, documentStore);
            IndexCreation.CreateIndexes(typeof(SagaAudit.SagaSnapshot).Assembly, documentStore);

            PurgeKnownEndpointsWithTemporaryIdsThatAreDuplicate(documentStore);
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

        static SerializationBinder MigratedTypeAwareBinder = new MigratedTypeAwareBinder();
    }
}