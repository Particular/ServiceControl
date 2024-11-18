﻿namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Threading.Tasks;
    using System.Xml.Serialization;
    using Accounts;
    using FileSystem;
    using NuGet.Versioning;
    using Queues;
    using ReportCard;
    using Services;
    using UrlAcl;
    using Validation;

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
                //RavenDB when provided with localhost as the hostname will try to open ports on all interfaces
                //by using + http binding. This in turn requires a matching UrlAcl registration.
                var baseUrl = string.Equals("localhost", HostName, StringComparison.OrdinalIgnoreCase)
                    ? $"http://+:{DatabaseMaintenancePort}/"
                    : $"http://{HostName}:{DatabaseMaintenancePort}/";

                return baseUrl;
            }
        }

        string[] FlagFiles
        {
            get
            {
                const string flagFileName = ".notconfigured";
                return
                [
                    Path.Combine(InstallPath, flagFileName),
                    Path.Combine(DBPath, flagFileName),
                    Path.Combine(LogPath, flagFileName)
                ];
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

        public string InstanceName { get; set; }

        public string DisplayName { get; set; }

        public bool SkipQueueCreation { get; set; }

        public bool EnableFullTextSearchOnBodies { get; set; }

        [XmlIgnore]
        public SemanticVersion Version { get; set; }

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

        public string BrowsableUrl => throw new NotImplementedException("Not available until the instance is installed");

        public virtual void CopyFiles(string zipResourceName)
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
            FileUtils.UnzipToSubdirectory(zipResourceName, InstallPath);
            FileUtils.UnzipToSubdirectory("InstanceShared.zip", InstallPath);
            FileUtils.UnzipToSubdirectory("RavenDBServer.zip", Path.Combine(InstallPath, "Persisters", "RavenDB", "RavenDBServer"));
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
            var dependencies = new List<string>()
            {
                "HTTP"
            };

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

        public void RemoveUrlAcl()
        {
            var reservation = new UrlReservation(AclUrl, new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));
            reservation.Delete();

            var maintenanceReservation = new UrlReservation(AclMaintenanceUrl, new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));
            maintenanceReservation.Delete();
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

        public async Task Validate(Func<PathInfo, Task<bool>> promptToProceed)
        {
            RunValidation(ValidateTransport);
            RunValidation(ValidatePort);
            RunValidation(ValidateMaintenancePort);

            try
            {
                ReportCard.CancelRequested = await ValidatePaths(promptToProceed).ConfigureAwait(false);
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

        protected virtual Task<bool> ValidatePaths(Func<PathInfo, Task<bool>> promptToProceed)
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