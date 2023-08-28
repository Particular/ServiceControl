namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Xml;
    using System.Xml.Serialization;
    using Configuration.ServiceControl;
    using FileSystem;
    using Queues;
    using Services;
    using Validation;

    public class ServiceControlNewInstance : ServiceControlInstallableBase, IServiceControlInstance
    {
        public static ServiceControlNewInstance CreateWithDefaultPersistence()
        {
            return CreateWithDefaultPersistence(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        }

        public static ServiceControlNewInstance CreateWithDefaultPersistence(string deploymentCachePath)
        {
            return CreateWithPersistence(deploymentCachePath, DefaultPersister);
        }

        public static ServiceControlNewInstance CreateWithPersistence(string deploymentCachePath, string persistence)
        {
            var zipInfo = ServiceControlZipInfo.Find(deploymentCachePath);
            var persistenceManifest = ServiceControlPersisters.LoadAllManifests(zipInfo.FilePath)
                .Single(manifest => manifest.Name == persistence);

            return new ServiceControlNewInstance(zipInfo.Version, persistenceManifest);
        }

        public ServiceControlNewInstance(Version version, PersistenceManifest persistenceManifest)
        {
            Version = version;
            PersistenceManifest = persistenceManifest;
        }

        public override void WriteConfigurationFile()
        {
            var appConfig = new ServiceControlAppConfig(this);
            appConfig.Save();
        }

        public PersistenceManifest PersistenceManifest { get; }
        public override string DirectoryName => "ServiceControl";

        public List<RemoteInstanceSetting> RemoteInstances { get; set; } = new List<RemoteInstanceSetting>();

        public void AddRemoteInstance(string apiUri)
        {
            //Secondary instance can be configured with * or + as the hostname to ensure
            //that the api is bound to all available network interfaces. This is problematic
            //from the main instance perspective as it requires concrete hostname.
            //Remote instances settings are added only via UI when all instances are deployed
            //on the same machine so it is safe to replace * or + with localhost.
            if (apiUri.Contains("*") || apiUri.Contains("+"))
            {
                apiUri = apiUri.Replace("*", "localhost")
                    .Replace("+", "localhost");
            }

            if (RemoteInstances.All(x => string.Compare(x.ApiUri, apiUri, StringComparison.InvariantCultureIgnoreCase) != 0))
            {
                RemoteInstances.Add(new RemoteInstanceSetting
                {
                    ApiUri = apiUri
                });
            }
        }

        [XmlElement(typeof(XmlNullableTimeSpan))]
        public TimeSpan? AuditRetentionPeriod { get; set; }

        [XmlElement(typeof(XmlTimeSpan))]
        public TimeSpan ErrorRetentionPeriod { get; set; }

        internal override WindowsServiceDetails GetWindowsServiceDetails()
        {
            return new WindowsServiceDetails
            {
                ServiceAccount = ServiceAccount,
                ServiceAccountPwd = ServiceAccountPwd,
                DisplayName = DisplayName,
                Name = Name,
                ImagePath = $"\"{Path.Combine(InstallPath, Constants.ServiceControlExe)}\" --serviceName={Name}",
                ServiceDescription = ServiceDescription
            };
        }

        protected override void RunQueueCreation()
        {
            QueueCreation.RunQueueCreation(this);
        }

        protected override void ValidateMaintenancePort()
        {
            DatabaseMaintenancePortValidator.Validate(this);
        }

        protected override void ValidateQueueNames()
        {
            QueueNameValidator.Validate(this);
        }

        protected override void ValidateServiceAccount()
        {
            ServiceAccountValidation.Validate(this);
        }

        protected override void ValidateConnectionString()
        {
            ConnectionStringValidator.Validate(this);
        }

        public static ServiceControlNewInstance Load(string path)
        {
            ServiceControlNewInstance instanceData;
            var serializer = new XmlSerializer(typeof(ServiceControlNewInstance));
            using (var stream = File.OpenRead(path))
            {
                instanceData = (ServiceControlNewInstance)serializer.Deserialize(stream);
            }

            var doc = new XmlDocument();
            doc.Load(path);
            if (doc.SelectSingleNode("/ServiceControlInstanceMetadata/ForwardErrorMessages") == null)
            {
                throw new InvalidDataException("The supplied file is using an old format. Which is no longer supported.");
            }

            if (doc.SelectSingleNode("/ServiceControlInstanceMetadata/AuditRetentionPeriod") == null)
            {
                throw new InvalidDataException("The supplied file is using an old format. Which is no longer supported.");
            }

            if (doc.SelectSingleNode("/ServiceControlInstanceMetadata/ErrorRetentionPeriod") == null)
            {
                throw new InvalidDataException("The supplied file is using an old format. Which is no longer supported.");
            }

            return instanceData;
        }

        // TODO: Change after Raven5 introduced
        public const string DefaultPersister = "RavenDB35";
    }
}