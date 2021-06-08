namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.ComponentModel.Composition.Hosting;
    using System.IO;
    using System.Runtime.Serialization;
    using NServiceBus.Logging;
    using Particular.Licensing;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using Raven.Client.Document;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;
    using Subscriptions;

    class RavenBootstrapper
    {
        public static Settings Settings { get; set; }

        public static string ReadLicense()
        {
            using (var resourceStream = typeof(RavenBootstrapper).Assembly.GetManifestResourceStream("ServiceControl.Infrastructure.RavenDB.RavenLicense.xml"))
            using (var reader = new StreamReader(resourceStream))
            {
                return reader.ReadToEnd();
            }
        }

        public static void Configure(EmbeddableDocumentStore documentStore, Settings settings, bool maintenanceMode = false)
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

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenBootstrapper));

        static SerializationBinder MigratedTypeAwareBinder = new MigratedTypeAwareBinder();
    }
}