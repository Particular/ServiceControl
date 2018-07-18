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
    using ServiceControlInstaller.Engine.Accounts;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.FileSystem;
    using ServiceControlInstaller.Engine.Queues;
    using ServiceControlInstaller.Engine.ReportCard;
    using ServiceControlInstaller.Engine.Services;
    using ServiceControlInstaller.Engine.UrlAcl;
    using ServiceControlInstaller.Engine.Validation;

    public class ServiceControlNewInstance : IServiceControlInstance
    {
        public string LogPath { get; set; }
        public string DBPath { get; set; }
        public string HostName { get; set; }

        [XmlElement(typeof(XmlTimeSpan))]
        public TimeSpan AuditRetentionPeriod { get; set; }

        [XmlElement(typeof(XmlTimeSpan))]
        public TimeSpan ErrorRetentionPeriod { get; set; }

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
        public string ServiceDescription { get; set; }
        public bool SkipQueueCreation { get; set; }
        public bool IsUpdatingDataStore { get; set; }


        [XmlIgnore]
        public Version Version { get; set; }
        [XmlIgnore]
        public string ServiceAccount { get; set; }
        [XmlIgnore]
        public string ServiceAccountPwd { get; set; }
        [XmlIgnore]
        public ReportCard ReportCard { get; set; }

        public ServiceControlNewInstance()
        {
            var appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var zipInfo = ServiceControlZipInfo.Find(appDirectory);
            Version = zipInfo.Version;
        }

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
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, "ServiceControl");
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, $@"Transports\{TransportPackage.ZipName}");
        }

        public void WriteConfigurationFile()
        {
            var appConfig = new AppConfig(this);
            appConfig.Save();
        }

        public void RegisterService()
        {
            var serviceDetails = new WindowsServiceDetails
            {
                ServiceAccount = ServiceAccount,
                ServiceAccountPwd = ServiceAccountPwd,
                DisplayName = DisplayName,
                Name = Name,
                ImagePath = $"\"{Path.Combine(InstallPath, Constants.ServiceControlExe)}\" --serviceName={Name}",
                ServiceDescription = ServiceDescription
            };
            var dependencies = new List<string>();
            if (TransportPackage.ZipName.Equals("MSMQ", StringComparison.OrdinalIgnoreCase))
            {
                dependencies.Add("MSMQ");
            }
            WindowsServiceController.RegisterNewService(serviceDetails, dependencies.ToArray());

            // Service registered so pull out not configured flag files.
            RemoveFlagFiles();
        }

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
                QueueCreation.RunQueueCreation(this);
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

        public static ServiceControlNewInstance Load(string path)
        {
            ServiceControlNewInstance instanceData;
            var serializer = new XmlSerializer(typeof(ServiceControlNewInstance));
            using (var stream = File.OpenRead(path))
            {
                instanceData = (ServiceControlNewInstance) serializer.Deserialize(stream);
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

        public void Validate(Func<PathInfo, bool> promptToProceed)
        {
            if (TransportPackage.ZipName.Equals("MSMQ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    MsmqConfigValidator.Validate();
                }
                catch(EngineValidationException ex)
                {
                    ReportCard.Errors.Add(ex.Message);
                }
            }

            try
            {
                PortValidator.Validate(this);
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }

            try
            {
                DatabaseMaintenancePortValidator.Validate(this);
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }

            try
            {
                ReportCard.CancelRequested = new PathsValidator(this).RunValidation(true, promptToProceed);
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }

            try
            {
                ServiceControlQueueNameValidator.Validate(this);
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }

            try
            {
                CheckForConflictingUrlAclReservations();
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }

            try
            {
                ServiceAccountValidation.Validate(this);
                ConnectionStringValidator.Validate(this);
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
                    File.Delete(flagFile);
            }
        }

        void AddFlagFiles()
        {
            foreach (var flagFile in FlagFiles)
            {
                if (!File.Exists(flagFile))
                    File.CreateText(flagFile).Close();
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
                    Path.Combine(LogPath, flagFileName),
                };
            }
        }

        public string BrowsableUrl
        {
            get
            {
                throw new NotImplementedException("Not available until the instance is installed");
            } 
        }
    }
}