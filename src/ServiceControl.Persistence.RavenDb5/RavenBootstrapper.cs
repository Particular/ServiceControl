namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.ComponentModel.Composition.Hosting;
    using System.Globalization;
    using System.IO;
    using System.Runtime.Serialization;
    using ByteSizeLib;
    using NServiceBus.Logging;
    using ServiceControl.Infrastructure.RavenDB;
    using ServiceControl.Infrastructure.RavenDB.Subscriptions;

    static class RavenBootstrapper
    {
        public const string DatabasePathKey = "DbPath";
        public const string HostNameKey = "HostName";
        public const string DatabaseMaintenancePortKey = "DatabaseMaintenancePort";
        public const string ExposeRavenDBKey = "ExposeRavenDB";
        public const string ExpirationProcessTimerInSecondsKey = "ExpirationProcessTimerInSeconds";
        public const string RunInMemoryKey = "RavenDB35/RunInMemory";
        public const string ConnectionStringKey = "RavenDB5/ConnectionString";
        public const string MinimumStorageLeftRequiredForIngestionKey = "MinimumStorageLeftRequiredForIngestion";
        public const string DatabaseNameKey = "RavenDB5/DatabaseName";

        public static RavenDBPersisterSettings Settings { get; private set; }

        public static void Configure(EmbeddableDocumentStore documentStore, RavenDBPersisterSettings settings)
        {
            Settings = settings;

            var runInMemory = settings.RunInMemory;

            if (runInMemory)
            {
                documentStore.RunInMemory = true;
            }
            else
            {
                var dbPath = settings.DatabasePath;

                if (string.IsNullOrEmpty(dbPath))
                {
                    throw new InvalidOperationException($"{DatabasePathKey} is mandatory");
                }

                Directory.CreateDirectory(dbPath);

                documentStore.DataDirectory = dbPath;
                documentStore.Configuration.CompiledIndexCacheDirectory = dbPath;
            }

            var exposeRavenDB = settings.ExposeRavenDB;

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

            documentStore.Configuration.DisableClusterDiscovery = true;
            documentStore.Configuration.ResetIndexOnUncleanShutdown = true;

            if (settings.DatabaseMaintenancePort == 0)
            {
                throw new Exception($"{DatabaseMaintenancePortKey} is mandatory.");
            }

            documentStore.Configuration.Port = settings.DatabaseMaintenancePort;

            if (string.IsNullOrEmpty(settings.HostName))
            {
                throw new Exception($"{HostNameKey} is mandatory.");
            }

            var hostName = settings.HostName;

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

            if (settings.MaintenanceMode)
            {
                Logger.InfoFormat($"RavenDB is now accepting requests on {settings.DatabaseMaintenanceUrl}");
            }

            if (settings.RunInMemory == false)
            {
                RecordStartup();
            }
        }

        static void RecordStartup()
        {
            var dataSize = DataSize();
            var folderSize = FolderSize();

            var startupMessage = $@"
-------------------------------------------------------------
Database Size:                      {ByteSize.FromBytes(dataSize).ToString("#.##", CultureInfo.InvariantCulture)}
Database Folder Size:               {ByteSize.FromBytes(folderSize).ToString("#.##", CultureInfo.InvariantCulture)}
-------------------------------------------------------------";

            Logger.Info(startupMessage);
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

        static long DataSize()
        {
            var datafilePath = Path.Combine(Settings.DatabasePath, "data");

            try
            {
                var info = new FileInfo(datafilePath);
                if (!info.Exists)
                {
                    return -1;
                }
                return info.Length;
            }
            catch
            {
                return -1;
            }
        }

        static long FolderSize()
        {
            try
            {
                var dir = new DirectoryInfo(Settings.DatabasePath);
                var dirSize = DirSize(dir);
                return dirSize;
            }
            catch
            {
                return -1;
            }
        }

        static long DirSize(DirectoryInfo d)
        {
            long size = 0;
            if (d.Exists)
            {
                FileInfo[] fis = d.GetFiles();
                foreach (FileInfo fi in fis)
                {
                    size += fi.Length;
                }

                DirectoryInfo[] dis = d.GetDirectories();
                foreach (DirectoryInfo di in dis)
                {
                    size += DirSize(di);
                }
            }

            return size;
        }

        static readonly SerializationBinder MigratedTypeAwareBinder = new MigratedTypeAwareBinder();

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenBootstrapper));

    }
}
