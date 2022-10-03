namespace ServiceControlInstaller.Engine.Instances
{
    using System;
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

    public class ServiceControlAuditNewInstance : ServiceControlInstallableBase, IServiceControlAuditInstance
    {
        public static ServiceControlAuditNewInstance CreateWithDefaultPersistence()
        {
            return CreateWithDefaultPersistence(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        }

        public static ServiceControlAuditNewInstance CreateWithDefaultPersistence(string deploymentCachePath)
        {
            var zipInfo = ServiceControlAuditZipInfo.Find(deploymentCachePath);
            var persistenceManifest = ServiceControlAuditPersisters.LoadAllManifests(zipInfo.FilePath)
                .Single(manifest => manifest.Name == "RavenDb5");

            return new ServiceControlAuditNewInstance(zipInfo.Version, persistenceManifest);
        }

        public ServiceControlAuditNewInstance(Version version, PersistenceManifest persistenceManifest)
        {
            Version = version;
            PersistenceManifest = persistenceManifest;
        }

        public string ServiceControlQueueAddress { get; set; }
        public PersistenceManifest PersistenceManifest { get; }

        public override void WriteConfigurationFile()
        {
            var appConfig = new ServiceControlAuditAppConfig(this);
            appConfig.Save();
        }

        public override string DirectoryName => "ServiceControl.Audit";

        [XmlElement(typeof(XmlTimeSpan))]
        public TimeSpan AuditRetentionPeriod { get; set; }

        internal override WindowsServiceDetails GetWindowsServiceDetails()
        {
            return new WindowsServiceDetails
            {
                ServiceAccount = ServiceAccount,
                ServiceAccountPwd = ServiceAccountPwd,
                DisplayName = DisplayName,
                Name = Name,
                ImagePath = $"\"{Path.Combine(InstallPath, Constants.ServiceControlAuditExe)}\" --serviceName={Name}",
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

        public override void CopyFiles(string zipFilePath)
        {
            base.CopyFiles(zipFilePath);

            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, $@"Persisters\{PersistenceManifest.Name}");
        }
    }
}