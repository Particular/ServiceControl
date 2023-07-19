﻿namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.ComponentModel.Composition.Hosting;
    using System.IO;
    using System.Runtime.Serialization;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client.Document;
    using Raven.Client.Embedded;
    using ServiceControl.Infrastructure.RavenDB;
    using ServiceControl.Infrastructure.RavenDB.Subscriptions;

    static class RavenBootstrapper
    {
        public const string DatabasePathKey = "DbPath";
        public const string HostNameKey = "HostName";
        public const string DatabaseMaintenancePortKey = "DatabaseMaintenancePort";
        public const string ExposeRavenDBKey = "ExposeRavenDB";
        public const string ExpirationProcessTimerInSecondsKey = "ExpirationProcessTimerInSeconds";
        public const string ExpirationProcessBatchSizeKey = "ExpirationProcessBatchSize";
        public const string RunCleanupBundleKey = "RavenDB35/RunCleanupBundle";
        public const string RunInMemoryKey = "RavenDB35/RunInMemory";
        public const string MinimumStorageLeftRequiredForIngestionKey = "MinimumStorageLeftRequiredForIngestion";


        public static PersistenceSettings Settings { get; private set; }

        public static void Configure(EmbeddableDocumentStore documentStore, PersistenceSettings settings)
        {
            Settings = settings;

            var runInMemory = false;
            if (settings.PersisterSpecificSettings.TryGetValue(RunInMemoryKey, out var runInMemoryString))
            {
                runInMemory = bool.Parse(runInMemoryString);
            }

            if (runInMemory)
            {
                documentStore.RunInMemory = true;
            }
            else
            {
                if (!settings.PersisterSpecificSettings.TryGetValue(DatabasePathKey, out string dbPath))
                {
                    throw new InvalidOperationException($"{DatabasePathKey} is mandatory");
                }

                Directory.CreateDirectory(dbPath);

                documentStore.DataDirectory = dbPath;
                documentStore.Configuration.CompiledIndexCacheDirectory = dbPath;
                documentStore.Listeners.RegisterListener(new SubscriptionsLegacyAddressConverter());
            }

            var exposeRavenDB = false;
            if (settings.PersisterSpecificSettings.TryGetValue(ExposeRavenDBKey, out var exposeRavenDBString))
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

            var runCleanupBundle = true;
            if (settings.PersisterSpecificSettings.TryGetValue(RunCleanupBundleKey, out var runCleanupBundleString))
            {
                runCleanupBundle = bool.Parse(runCleanupBundleString);
            }

            if (runCleanupBundle)
            {
                documentStore.Configuration.Settings.Add("Raven/ActiveBundles", "CustomDocumentExpiration");
            }

            documentStore.Configuration.DisableClusterDiscovery = true;
            documentStore.Configuration.ResetIndexOnUncleanShutdown = true;

            if (!settings.PersisterSpecificSettings.TryGetValue(DatabaseMaintenancePortKey, out var databaseMaintenancePort))
            {
                throw new Exception($"{DatabaseMaintenancePortKey} is mandatory.");
            }

            documentStore.Configuration.Port = int.Parse(databaseMaintenancePort);

            if (!settings.PersisterSpecificSettings.TryGetValue(HostNameKey, out var hostName))
            {
                throw new Exception($"{HostNameKey} is mandatory.");
            }

            documentStore.Configuration.HostName = hostName == "*" || hostName == "+"
                ? "localhost"
                : hostName;
            documentStore.Conventions.SaveEnumsAsIntegers = true;
            documentStore.Conventions.CustomizeJsonSerializer = serializer => serializer.Binder = MigratedTypeAwareBinder;
            documentStore.Configuration.Catalog.Catalogs.Add(new AssemblyCatalog(typeof(RavenBootstrapper).Assembly));
            documentStore.Conventions.FindClrType = (id, doc, metadata) =>
            {
                var clrtype = metadata.Value<string>(Constants.RavenClrType);

                // The CLR type cannot be assumed to be always there
                if (clrtype == null)
                {
                    return null;
                }

                if (clrtype.EndsWith(".Subscription, NServiceBus.Core"))
                {
                    clrtype = ReflectionUtil.GetFullNameWithoutVersionInformation(typeof(Subscription));
                }
                else if (clrtype.EndsWith(".Subscription, NServiceBus.RavenDB"))
                {
                    clrtype = ReflectionUtil.GetFullNameWithoutVersionInformation(typeof(Subscription));
                }

                return clrtype;
            };
        }

        public static string ReadLicense()
        {
            using (var resourceStream = typeof(RavenBootstrapper).Assembly.GetManifestResourceStream("ServiceControl.Persistence.RavenDb.RavenLicense.xml"))
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

        static SerializationBinder MigratedTypeAwareBinder = new MigratedTypeAwareBinder();

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenBootstrapper));

    }
}
