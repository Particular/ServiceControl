namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.ComponentModel.Composition.Hosting;
    using System.IO;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using Particular.ServiceControl.Licensing;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.EndpointControl;
    using ServiceControl.Infrastructure.RavenDB.Subscriptions;

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

        public void Customize(EndpointConfiguration configuration)
        {
            var documentStore = configuration.GetSettings().Get<EmbeddableDocumentStore>();
            var settings = configuration.GetSettings().Get<Settings>("ServiceControl.Settings");
            var markerFileService = configuration.GetSettings().Get<MarkerFileService>("ServiceControl.MarkerFileService");

            Settings = settings;

            StartRaven(documentStore, settings, markerFileService, false);

            configuration.UsePersistence<CachedRavenDBPersistence, StorageType.Subscriptions>();
        }

        public static Settings Settings { get; set; }

        public void StartRaven(EmbeddableDocumentStore documentStore, Settings settings, MarkerFileService markerFileService, bool maintenanceMode)
        {
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
                documentStore.Configuration.Settings["Raven/License"] = NonLockingFileReader.ReadAllTextWithoutLocking(localRavenLicense);
            }
            else
            {
                Logger.InfoFormat("Loading Embedded RavenDB license");
                documentStore.Configuration.Settings["Raven/License"] = ReadLicense();
            }

            //This is affects only remote access to the database in maintenace mode and enables access without authentication
            documentStore.Configuration.Settings["Raven/AnonymousAccess"] = "Admin";
            documentStore.Configuration.Settings["Raven/Licensing/AllowAdminAnonymousAccessForCommercialUse"] = "true";

            if (!maintenanceMode)
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

            documentStore.Configuration.Catalog.Catalogs.Add(new AssemblyCatalog(GetType().Assembly));

            using (markerFileService.CreateMarker("datamigration.marker"))
            {
                documentStore.Initialize();
            }

            Logger.Info("Index creation started");

            IndexCreation.CreateIndexes(typeof(RavenBootstrapper).Assembly, documentStore);

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
    }
}
