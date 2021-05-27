namespace ServiceControl.Audit.Infrastructure.RavenDB
{
    using System;
    using System.ComponentModel.Composition.Hosting;
    using System.IO;
    using System.Runtime.Serialization;
    using NServiceBus.Logging;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using ServiceControl.SagaAudit;
    using Settings;

    class RavenBootstrapper
    {
        public static Settings Settings { get; private set; }

        public static void ConfigureAndStart(EmbeddableDocumentStore documentStore, Settings settings, bool maintenanceMode = false)
        {
            Settings = settings;

            Directory.CreateDirectory(settings.DbPath);

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

            //This is affects only remote access to the database in maintenance mode and enables access without authentication
            documentStore.Configuration.Settings["Raven/AnonymousAccess"] = "Admin";
            documentStore.Configuration.Settings["Raven/Licensing/AllowAdminAnonymousAccessForCommercialUse"] = "true";

            if (settings.RunCleanupBundle)
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

            documentStore.Configuration.Catalog.Catalogs.Add(new AssemblyCatalog(typeof(RavenBootstrapper).Assembly));

            documentStore.Initialize();

            Logger.Info("Index creation started");

            IndexCreation.CreateIndexes(typeof(RavenBootstrapper).Assembly, documentStore);
            IndexCreation.CreateIndexes(typeof(SagaSnapshot).Assembly, documentStore);
        }

        static string ReadLicense()
        {
            using (var resourceStream = typeof(RavenBootstrapper).Assembly.GetManifestResourceStream("ServiceControl.Audit.Infrastructure.RavenDB.RavenLicense.xml"))
            using (var reader = new StreamReader(resourceStream))
            {
                return reader.ReadToEnd();
            }
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

        static readonly SerializationBinder MigratedTypeAwareBinder = new MigratedTypeAwareBinder();
    }
}