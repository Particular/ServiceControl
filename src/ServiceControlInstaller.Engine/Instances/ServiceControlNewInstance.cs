// ReSharper disable MemberCanBePrivate.Global

namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Xml;
    using System.Xml.Serialization;
    using Accounts;
    using Configuration.ServiceControl;
    using FileSystem;
    using Queues;
    using ReportCard;
    using Services;
    using UrlAcl;
    using Validation;

    public class ServiceControlNewInstance : ServiceControlInstallableBase, IServiceControlInstance
    {
        public ServiceControlNewInstance()
        {
            var appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var zipInfo = ServiceControlZipInfo.Find(appDirectory);
            Version = zipInfo.Version;
        }

        public override void WriteConfigurationFile()
        {
            var appConfig = new ServiceControlAppConfig(this);
            appConfig.Save();
        }

        public override string DirectoryName => "ServiceControl";

        [XmlElement(typeof(XmlTimeSpan))]
        public TimeSpan AuditRetentionPeriod { get; set; } = TimeSpan.FromHours(1);
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
                throw new InvalidDataException("The supplied file is using an old format. Use 'New-ServiceControlUnattendedFile' from the ServiceControl to create a new unattended install file.");
            }

            if (doc.SelectSingleNode("/ServiceControlInstanceMetadata/AuditRetentionPeriod") == null)
            {
                throw new InvalidDataException("The supplied file is using an old format. Use 'New-ServiceControlUnattendedFile' from the ServiceControl to create a new unattended install file.");
            }

            if (doc.SelectSingleNode("/ServiceControlInstanceMetadata/ErrorRetentionPeriod") == null)
            {
                throw new InvalidDataException("The supplied file is using an old format. Use 'New-ServiceControlUnattendedFile' from the ServiceControl to create a new unattended install file.");
            }

            return instanceData;
        }
    }

    public class ServiceControlAuditNewInstance : ServiceControlInstallableBase, IServiceControlAuditInstance
    {
        public ServiceControlAuditNewInstance()
        {
            var appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var zipInfo = ServiceControlZipInfo.Find(appDirectory);
            Version = zipInfo.Version;
        }

        public string ServiceControlQueueAddress { get; set; }

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

    public abstract class ServiceControlInstallableBase : IHttpInstance, IServiceControlPaths, ITransportConfig
    {
        public string ServiceDescription { get; set; }

        public abstract string DirectoryName { get; }

        [XmlIgnore]
        public ReportCard ReportCard { get; set; }

        public string AclUrl
        {
            get
            {
                var baseUrl = $"http://{HostName}:{Port}/";
                if (string.IsNullOrWhiteSpace(VirtualDirectory))
                {
                    return baseUrl;
                }

                return $"{baseUrl}{VirtualDirectory}{(VirtualDirectory.EndsWith("/") ? string.Empty : "/")}";
            }
        }

        public string AclMaintenanceUrl
        {
            get
            {
                var baseUrl = $"http://{HostName}:{DatabaseMaintenancePort}/";
                return baseUrl;
            }
        }

        string[] FlagFiles
        {
            get
            {
                const string flagFileName = ".notconfigured";
                return new[]
                {
                    Path.Combine(InstallPath, flagFileName),
                    Path.Combine(DBPath, flagFileName),
                    Path.Combine(LogPath, flagFileName)
                };
            }
        }

        public string LogPath { get; set; }
        public string DBPath { get; set; }
        public string HostName { get; set; }
        public string InstallPath { get; set; }
        public int Port { get; set; }
        public int? DatabaseMaintenancePort { get; set; }
        public string VirtualDirectory { get; set; }
        public string ErrorQueue { get; set; }
        public string ErrorLogQueue { get; set; }
        public string AuditQueue { get; set; }
        public string AuditLogQueue { get; set; }
        public bool ForwardAuditMessages { get; set; }
        public bool ForwardErrorMessages { get; set; }
        public TransportInfo TransportPackage { get; set; }
        public string ConnectionString { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public bool SkipQueueCreation { get; set; }


        [XmlIgnore]
        public Version Version { get; set; }

        [XmlIgnore]
        public string ServiceAccount { get; set; }

        [XmlIgnore]
        public string ServiceAccountPwd { get; set; }

        public string Url
        {
            get
            {
                if (string.IsNullOrWhiteSpace(VirtualDirectory))
                {
                    return $"http://{HostName}:{Port}/api/";
                }

                return $"http://{HostName}:{Port}/{VirtualDirectory}{(VirtualDirectory.EndsWith("/") ? string.Empty : "/")}api/";
            }
        }

        public string BrowsableUrl
        {
            get { throw new NotImplementedException("Not available until the instance is installed"); }
        }

        public void CopyFiles(string zipFilePath)
        {
            //Clear out any files from previos runs of Add Instance, just in case user switches transport
            //Validation checks for the flag file so wont get here if the directory was also changed
            FileUtils.DeleteDirectory(InstallPath, true, true);

            var account = new NTAccount(UserAccount.ParseAccountName(ServiceAccount).QualifiedName);
            var readExecuteAccessRule = new FileSystemAccessRule(account, FileSystemRights.ReadAndExecute | FileSystemRights.Traverse | FileSystemRights.ListDirectory, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow);
            FileUtils.CreateDirectoryAndSetAcl(InstallPath, readExecuteAccessRule);

            var modifyAccessRule = new FileSystemAccessRule(account, FileSystemRights.Modify | FileSystemRights.Traverse | FileSystemRights.ListDirectory, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow);
            if (!string.IsNullOrWhiteSpace(LogPath))
            {
                FileUtils.CreateDirectoryAndSetAcl(LogPath, modifyAccessRule);
            }

            if (!string.IsNullOrWhiteSpace(DBPath))
            {
                FileUtils.CreateDirectoryAndSetAcl(DBPath, modifyAccessRule);
            }

            // Mark these directories with a flag
            // These flags indicate the directory is empty check can be ignored
            // We need this because if an install screws up and doesn't complete it is ok to overwrite on a subsequent attempt
            // First run will still the check
            AddFlagFiles();

            // Copy the binaries from a zip
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, DirectoryName);
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, $@"Transports\{TransportPackage.ZipName}");
        }

        public virtual void WriteConfigurationFile()
        {
        }

        public void RegisterService()
        {
            var serviceDetails = GetWindowsServiceDetails();
            var dependencies = GetServiceDependencies();

            WindowsServiceController.RegisterNewService(serviceDetails, dependencies.ToArray());

            // Service registered so pull out not configured flag files.
            RemoveFlagFiles();
        }

        protected List<string> GetServiceDependencies()
        {
            var dependencies = new List<string>();
            if (TransportPackage.ZipName.Equals("MSMQ", StringComparison.OrdinalIgnoreCase))
            {
                dependencies.Add("MSMQ");
            }

            return dependencies;
        }

        internal abstract WindowsServiceDetails GetWindowsServiceDetails();

        protected abstract void RunQueueCreation();

        public void RegisterUrlAcl()
        {
            var reservation = new UrlReservation(AclUrl, new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));
            reservation.Create();

            var maintenanceReservation = new UrlReservation(AclMaintenanceUrl, new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));
            maintenanceReservation.Create();
        }

        public void SetupInstance()
        {
            try
            {
                RunQueueCreation();
            }
            catch (QueueCreationFailedException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }
            catch (QueueCreationTimeoutException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }
        }

        public void Save(string path)
        {
            var serializer = new XmlSerializer(GetType());
            using (var stream = File.OpenWrite(path))
            {
                serializer.Serialize(stream, this);
            }
        }

        public void Validate(Func<PathInfo, bool> promptToProceed)
        {
            RunValidation(ValidateTransport);
            RunValidation(ValidatePort);
            RunValidation(ValidateMaintenancePort);

            try
            {
                ReportCard.CancelRequested = ValidatePaths(promptToProceed);
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }

            RunValidation(ValidateQueueNames);
            RunValidation(CheckForConflictingUrlAclReservations);
            RunValidation(ValidateServiceAccount);
            RunValidation(ValidateConnectionString);
        }

        public void RunValidation(Action action)
        {
            try
            {
                action();
            }
            catch (IdentityNotMappedException)
            {
                ReportCard.Errors.Add("The service account specified does not exist");
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }
        }

        protected virtual void ValidateConnectionString()
        {
        }

        protected virtual void ValidateServiceAccount()
        {
        }

        protected virtual void ValidateQueueNames()
        {
        }

        protected virtual bool ValidatePaths(Func<PathInfo, bool> promptToProceed)
        {
            return new PathsValidator(this).RunValidation(true, promptToProceed);
        }

        protected virtual void ValidateMaintenancePort()
        {
        }

        protected virtual void ValidatePort()
        {
            PortValidator.Validate(this);
        }

        protected virtual void ValidateTransport()
        {
            if (TransportPackage.ZipName.Equals("MSMQ", StringComparison.OrdinalIgnoreCase))
            {
                MsmqConfigValidator.Validate();
            }
        }

        void CheckForConflictingUrlAclReservations()
        {
            foreach (var reservation in UrlReservation.GetAll().Where(p => p.Port == Port || p.Port == DatabaseMaintenancePort))
            {
                // exclusive or of reservation and instance - if only one of them has "localhost" then the UrlAcl will clash
                if (reservation.HostName.Equals("localhost", StringComparison.OrdinalIgnoreCase) && !HostName.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                    !reservation.HostName.Equals("localhost", StringComparison.OrdinalIgnoreCase) && HostName.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                {
                    throw new EngineValidationException($"Conflicting UrlAcls found - {Url} vs {reservation.Url}");
                }
            }
        }

        void RemoveFlagFiles()
        {
            foreach (var flagFile in FlagFiles)
            {
                if (File.Exists(flagFile))
                {
                    File.Delete(flagFile);
                }
            }
        }

        void AddFlagFiles()
        {
            foreach (var flagFile in FlagFiles)
            {
                if (!File.Exists(flagFile))
                {
                    File.CreateText(flagFile).Close();
                }
            }
        }
    }
}