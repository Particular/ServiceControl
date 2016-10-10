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
    using HttpApiWrapper;
    using ServiceControlInstaller.Engine.Configuration;
    using ServiceControlInstaller.Engine.FileSystem;
    using ServiceControlInstaller.Engine.Setup;
    using ServiceControlInstaller.Engine.ReportCard;
    using ServiceControlInstaller.Engine.Services;

    using ServiceControlInstaller.Engine.Validation;

    public class ServiceControlInstanceMetadata : IServiceControlInstance
    {
        public string LogPath { get; set; }
        public string DBPath { get; set; }
        public string BodyStoragePath { get; set; }
        public string IngestionCachePath { get; set; }
        public string HostName { get; set; }
        public TimeSpan AuditRetentionPeriod { get; set; }
        public TimeSpan ErrorRetentionPeriod { get; set; }
        public string InstallPath { get; set; }
        public int Port { get; set; }
        public string VirtualDirectory { get; set; }
        public string ErrorQueue { get; set; }
        public string ErrorLogQueue { get; set; }
        public string AuditQueue { get; set; }
        public string AuditLogQueue { get; set; }
        public bool ForwardAuditMessages { get; set; }
        public bool ForwardErrorMessages { get; set; }
        public string TransportPackage { get; set; }
        public string ConnectionString { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string ServiceDescription { get; set; }
        public Version Version { get; set; }
        public string Protocol =>  SslCert.GetThumbprint(Port) == null ? "http" : "https";
        public string ServiceAccount { get; set; }
        public string ServiceAccountPwd { get; set; }
        public ReportCard ReportCard { get; set; }

        public ServiceControlInstanceMetadata()
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
                    return $"{Protocol}://{HostName}:{Port}/api/";
                }
                return $"{Protocol}://{HostName}:{Port}/{VirtualDirectory}{(VirtualDirectory.EndsWith("/") ? String.Empty : "/")}api/";
            }
        }
        
        public string AclUrl
        {
            get
            {
                var baseUrl = $"{Protocol}://{HostName}:{Port}/";
                if (string.IsNullOrWhiteSpace(VirtualDirectory))
                {
                    return baseUrl;
                }
                return $"{baseUrl}{VirtualDirectory}{(VirtualDirectory.EndsWith("/") ? String.Empty : "/")}";
            }
        }

        public void CopyFiles(string zipFilePath)
        {
            //Clear out any files from previous runs of Add Instance, just in case user switches transport
            //Validation checks for the flag file so wont get here if the directory was also changed
            FileUtils.DeleteDirectory(InstallPath, true, true);
            var builtinUsers = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var readExecuteAccessRule = new FileSystemAccessRule(builtinUsers, FileSystemRights.ReadAndExecute | FileSystemRights.Traverse | FileSystemRights.ListDirectory, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow);
            FileUtils.CreateDirectoryAndSetAcl(InstallPath, readExecuteAccessRule);

            var modifyAccessRule = new FileSystemAccessRule(builtinUsers, FileSystemRights.Modify | FileSystemRights.Traverse | FileSystemRights.ListDirectory, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow);

            if (!string.IsNullOrWhiteSpace(LogPath))
            {
                FileUtils.CreateDirectoryAndSetAcl(LogPath, modifyAccessRule);
            }

            if (!string.IsNullOrWhiteSpace(DBPath))
            {
                FileUtils.CreateDirectoryAndSetAcl(DBPath, modifyAccessRule);
            }

            if (!string.IsNullOrWhiteSpace(BodyStoragePath))
            {
                FileUtils.CreateDirectoryAndSetAcl(BodyStoragePath, modifyAccessRule);
            }

            if (!string.IsNullOrWhiteSpace(IngestionCachePath))
            {
                FileUtils.CreateDirectoryAndSetAcl(IngestionCachePath, modifyAccessRule);
            }

            // Mark these directories with a flag 
            // These flags indicate the directory is empty check can be ignored
            // We need this because if an install screws up and doesn't complete it is ok to overwrite on a subsequent attempt
            // First run will still the check
            AddFlagFiles();

            // Copy the binaries from a zip
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, "ServiceControl");
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, $@"Transports\{TransportPackage}");
        }

        public void WriteConfigurationFile()
        {
            var configuration = new ConfigurationWriter(this);
            configuration.Validate();
            configuration.Save();
        }

        public void RegisterService()
        {
            var serviceDetails = new WindowsServiceDetails
            {
                ServiceAccount = ServiceAccount,
                ServiceAccountPwd = ServiceAccountPwd,
                DisplayName = DisplayName,
                Name = Name,
                ImagePath = $"\"{Path.Combine(InstallPath, "ServiceControl.exe")}\" --serviceName={Name}",
                ServiceDescription = ServiceDescription
            };
            var dependencies = new List<string>();
            if (TransportPackage.Equals("MSMQ", StringComparison.OrdinalIgnoreCase))
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
        }

        public void SetupInstance()
        {
            try
            {
                ServiceControlSetup.RunInSetupMode(this);
            }
            catch (ServiceControlSetupFailedException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }
            catch (ServiceControlSetupTimeoutException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }
        }

        public void Validate(Func<PathInfo, bool> promptToProceed)
        {
            if (TransportPackage.Equals("MSMQ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    MSMQConfigValidator.Validate();
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
                ReportCard.CancelRequested = PathsValidator.Validate(this, promptToProceed);
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }

            try
            {
                QueueNameValidator.Validate(this);
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
    }
}
