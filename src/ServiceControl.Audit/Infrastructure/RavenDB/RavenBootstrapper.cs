﻿namespace ServiceControl.Audit.Infrastructure.RavenDB
{
    using System;
    using System.ComponentModel.Composition.Hosting;
    using System.IO;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using Settings;
    using Subscriptions;

    class RavenBootstrapper : INeedInitialization
    {
        public static Settings Settings { get; set; }

        public bool RunCleanup { get; set; }

        public void Customize(EndpointConfiguration configuration)
        {
            var documentStore = configuration.GetSettings().Get<EmbeddableDocumentStore>();
            var settings = configuration.GetSettings().Get<Settings>("ServiceControl.Settings");

            Settings = settings;

            StartRaven(documentStore, settings, false);

            configuration.UsePersistence<CachedRavenDBPersistence, StorageType.Subscriptions>();
        }

        public static string ReadLicense()
        {
            using (var resourceStream = typeof(RavenBootstrapper).Assembly.GetManifestResourceStream("ServiceControl.Audit.Infrastructure.RavenDB.RavenLicense.xml"))
            using (var reader = new StreamReader(resourceStream))
            {
                return reader.ReadToEnd();
            }
        }

        public void StartRaven(EmbeddableDocumentStore documentStore, Settings settings, bool maintenanceMode)
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
                documentStore.Configuration.Settings["Raven/License"] = ReadAllTextWithoutLocking(localRavenLicense);
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

            documentStore.Configuration.Catalog.Catalogs.Add(new AssemblyCatalog(GetType().Assembly));

            documentStore.Initialize();

            Logger.Info("Index creation started");

            IndexCreation.CreateIndexes(typeof(RavenBootstrapper).Assembly, documentStore);
        }

        static string ReadAllTextWithoutLocking(string path)
        {
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var textReader = new StreamReader(fileStream))
            {
                return textReader.ReadToEnd();
            }
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenBootstrapper));
    }
}