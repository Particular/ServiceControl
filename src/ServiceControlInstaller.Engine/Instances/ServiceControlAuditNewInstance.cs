﻿namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.IO;
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
            const string persisterToUseForBrandNewInstances = StorageEngineNames.RavenDB;
            return CreateWithPersistence(persisterToUseForBrandNewInstances);
        }

        public static ServiceControlAuditNewInstance CreateWithPersistence(string persistence)
        {
            var persistenceManifest = ServiceControlPersisters.GetAuditPersistence(persistence);

            return new ServiceControlAuditNewInstance(persistenceManifest);
        }


        public ServiceControlAuditNewInstance(PersistenceManifest persistenceManifest)
        {
            Version = Constants.CurrentVersion;
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

        public override void CopyFiles(string zipResourceName)
        {
            base.CopyFiles(zipResourceName);

            FileUtils.UnzipToSubdirectory(zipResourceName, InstallPath, $@"Persisters\{PersistenceManifest.Name}");
        }
    }
}