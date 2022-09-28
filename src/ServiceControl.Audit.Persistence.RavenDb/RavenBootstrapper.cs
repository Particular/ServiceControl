namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.ComponentModel.Composition.Hosting;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Serialization;
    using NServiceBus.Logging;
    using Raven.Client.Embedded;
    using ServiceControl.Audit.Infrastructure.Migration;
    using ServiceControl.SagaAudit;

    class RavenBootstrapper
    {
        public static PersistenceSettings Settings { get; private set; }

        public static void Configure(EmbeddableDocumentStore documentStore, PersistenceSettings settings)
        {
            Settings = settings;

            var runInMemory = false;
            if (settings.PersisterSpecificSettings.TryGetValue("ServiceControl/Audit/RavenDb35/RunInMemory", out var runInMemoryString))
            {
                runInMemory = bool.Parse(runInMemoryString);
            }

            if (runInMemory)
            {
                documentStore.RunInMemory = true;
            }
            else
            {
                var dbPath = settings.PersisterSpecificSettings["ServiceControl.Audit/DbPath"];

                Directory.CreateDirectory(dbPath);

                documentStore.DataDirectory = dbPath;
                documentStore.Configuration.CompiledIndexCacheDirectory = dbPath;
            }

            var exposeRavenDB = false;

            if (settings.PersisterSpecificSettings.TryGetValue("ServiceControl.Audit/ExposeRavenDB", out var exposeRavenDBString))
            {
                exposeRavenDB = bool.Parse(exposeRavenDBString);
            }

            documentStore.UseEmbeddedHttpServer = settings.MaintenanceMode || exposeRavenDB;
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

            var runCleanupBundle = false;

            if (settings.PersisterSpecificSettings.TryGetValue("ServiceControl/Audit/RavenDb35/RunCleanupBundle", out var runCleanupBundleString))
            {
                runCleanupBundle = bool.Parse(runCleanupBundleString);
            }

            if (runCleanupBundle)
            {
                documentStore.Configuration.Settings.Add("Raven/ActiveBundles", "CustomDocumentExpiration");
            }

            documentStore.Configuration.DisableClusterDiscovery = true;
            documentStore.Configuration.ResetIndexOnUncleanShutdown = true;
            documentStore.Configuration.Port = int.Parse(settings.PersisterSpecificSettings["ServiceControl.Audit/DatabaseMaintenancePort"]);

            var hostName = settings.PersisterSpecificSettings["ServiceControl.Audit/HostName"];

            documentStore.Configuration.HostName = hostName == "*" || hostName == "+"
                ? "localhost"
                : hostName;
            documentStore.Conventions.SaveEnumsAsIntegers = true;
            documentStore.Conventions.CustomizeJsonSerializer = serializer => serializer.Binder = MigratedTypeAwareBinder;

            documentStore.Configuration.Catalog.Catalogs.Add(new AssemblyCatalog(typeof(RavenBootstrapper).Assembly));
        }

        static string ReadLicense()
        {
            using (var resourceStream = typeof(RavenBootstrapper).Assembly.GetManifestResourceStream("ServiceControl.Audit.Persistence.RavenDb.RavenLicense.xml"))
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

        public static Assembly[] IndexAssemblies =
        {
            typeof(RavenBootstrapper).Assembly, typeof(SagaDetailsIndex).Assembly
        };
    }
}