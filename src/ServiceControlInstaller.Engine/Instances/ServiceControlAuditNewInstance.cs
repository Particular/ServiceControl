namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.IO;
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
        public ServiceControlAuditNewInstance()
        {
            var appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var zipInfo = ServiceControlZipInfo.Find(appDirectory);
            Version = zipInfo.Version;

            //new instances defaults to RavenDb 5
            PersistenceType = "ServiceControl.Audit.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDb5";
        }

        public string ServiceControlQueueAddress { get; set; }
        public string PersistenceType { get; set; }

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

        public static ServiceControlAuditNewInstance Load(string path)
        {
            ServiceControlAuditNewInstance instanceData;
            var serializer = new XmlSerializer(typeof(ServiceControlAuditNewInstance));
            using (var stream = File.OpenRead(path))
            {
                instanceData = (ServiceControlAuditNewInstance)serializer.Deserialize(stream);
            }

            var doc = new XmlDocument();
            doc.Load(path);

            return instanceData;
        }
    }
}