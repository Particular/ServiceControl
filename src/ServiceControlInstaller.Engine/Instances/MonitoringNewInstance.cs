namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using Accounts;
    using Configuration.Monitoring;
    using FileSystem;
    using Queues;
    using ReportCard;
    using Services;
    using UrlAcl;
    using Validation;

    public class MonitoringNewInstance : IMonitoringInstance
    {
        public MonitoringNewInstance()
        {
            var appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var zipInfo = MonitoringZipInfo.Find(appDirectory);
            Version = zipInfo.Version;
        }

        public ReportCard ReportCard { get; set; }
        public string ServiceDescription { get; set; }

        string[] FlagFiles
        {
            get
            {
                const string flagFileName = ".notconfigured";
                return new[]
                {
                    Path.Combine(InstallPath, flagFileName),
                    Path.Combine(LogPath, flagFileName)
                };
            }
        }

        public string HostName { get; set; }
        public int Port { get; set; }

        public string ErrorQueue { get; set; }


        public string InstallPath { get; set; }
        public string LogPath { get; set; }

        public TransportInfo TransportPackage { get; set; }
        public string ConnectionString { get; set; }

        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string ServiceAccount { get; set; }
        public string ServiceAccountPwd { get; set; }
        public bool SkipQueueCreation { get; set; }


        public Version Version { get; }

        public string Url => $"http://{HostName}:{Port}/";

        public string BrowsableUrl
        {
            get { throw new NotImplementedException("Not available until the instance is installed"); }
        }

        public void CopyFiles(string zipFilePath)
        {
            //Clear out any files from previous runs of Add Instance, just in case user switches transport
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

            // Mark these directories with a flag 
            // These flags indicate the directory is empty check can be ignored
            // We need this because if an install screws up and doesn't complete it is ok to overwrite on a subsequent attempt
            // First run will still the check
            AddFlagFiles();

            // Copy the binaries from a zip
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, "ServiceControl.Monitoring");
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
                ImagePath = $"\"{Path.Combine(InstallPath, Constants.MonitoringExe)}\" --serviceName={Name}",
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
            var reservation = new UrlReservation(Url, new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));
            reservation.Create();
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

        public void Validate(Func<PathInfo, bool> promptToProceed)
        {
            if (TransportPackage.ZipName.Equals("MSMQ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    MsmqConfigValidator.Validate();
                }
                catch (EngineValidationException ex)
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
                ReportCard.CancelRequested = new PathsValidator(this).RunValidation(true, promptToProceed);
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }

            try
            {
                //TODO : QUEUE Validation
                // QueueNameValidator.Validate(this);
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
            foreach (var reservation in UrlReservation.GetAll().Where(p => p.Port == Port))
            {
                // exclusive or of reservation and instance - if only one of them has "localhost" then the UrlAcl will clash
                if ((reservation.HostName.Equals("localhost", StringComparison.OrdinalIgnoreCase) && !HostName.Equals("localhost", StringComparison.OrdinalIgnoreCase)) ||
                    (!reservation.HostName.Equals("localhost", StringComparison.OrdinalIgnoreCase) && HostName.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
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