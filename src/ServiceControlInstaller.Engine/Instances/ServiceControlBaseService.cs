namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using Accounts;
    using Configuration;
    using Configuration.ServiceControl;
    using FileSystem;
    using Queues;
    using ReportCard;
    using Services;
    using UrlAcl;
    using Validation;

    public abstract class ServiceControlBaseService : BaseService
    {
        protected ServiceControlBaseService(WindowsServiceController service)
        {
            Service = service;
            AppConfig = CreateAppConfig();
            Reload();
        }

        public bool InMaintenanceMode { get; set; }
        public ReportCard ReportCard { get; set; }

        /// <summary>
        /// Raven management URL
        /// </summary>
        public string StorageUrl
        {
            get
            {
                string host;
                switch (HostName)
                {
                    case "*":
                    case "+":
                        host = "localhost";
                        break;
                    default:
                        host = HostName;
                        break;
                }

                return $"http://{host}:{DatabaseMaintenancePort}/studio/index.html#databases/documents?&database=%3Csystem%3E";
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

        public string LogPath { get; set; }
        public string DBPath { get; set; }
        public string HostName { get; set; }
        public int Port { get; set; }
        public int? DatabaseMaintenancePort { get; set; }
        public string VirtualDirectory { get; set; }
        public string ErrorQueue { get; set; }
        public string AuditQueue { get; set; }
        public string ErrorLogQueue { get; set; }
        public string AuditLogQueue { get; set; }
        public bool ForwardAuditMessages { get; set; }
        public bool ForwardErrorMessages { get; set; }
        public TransportInfo TransportPackage { get; set; }
        public string ConnectionString { get; set; }
        public TimeSpan ErrorRetentionPeriod { get; set; }
        public bool SkipQueueCreation { get; set; }

        protected abstract string BaseServiceName { get; }

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
            get
            {
                string host;

                switch (HostName)
                {
                    case "*":
                        host = "localhost";
                        break;
                    case "+":
                        host = Environment.MachineName.ToLower();
                        break;
                    default:
                        host = HostName;
                        break;
                }

                if (string.IsNullOrWhiteSpace(VirtualDirectory))
                {
                    return $"http://{host}:{Port}/api/";
                }

                return $"http://{host}:{Port}/{VirtualDirectory}{(VirtualDirectory.EndsWith("/") ? string.Empty : "/")}api/";
            }
        }

        protected string ReadConnectionString()
        {
            if (File.Exists(Service.ExePath))
            {
                var configManager = ConfigurationManager.OpenExeConfiguration(Service.ExePath);
                var namedConnectionString = configManager.ConnectionStrings.ConnectionStrings["NServiceBus/Transport"];
                if (namedConnectionString != null)
                {
                    return namedConnectionString.ConnectionString;
                }
            }

            return null;
        }

        protected abstract string GetTransportTypeSetting();

        protected TransportInfo DetermineTransportPackage()
        {
            var transportAppSetting = GetTransportTypeSetting();
            var transport = ServiceControlCoreTransports.All.FirstOrDefault(p => p.Matches(transportAppSetting));
            if (transport != null)
            {
                return transport;
            }

            return ServiceControlCoreTransports.All.First(p => p.Default);
        }

        protected void RecreateUrlAcl(ServiceControlBaseService oldSettings)
        {
            oldSettings.RemoveUrlAcl();
            var reservation = new UrlReservation(AclUrl, new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));
            reservation.Create();

            if (oldSettings.Version.Major < 2) //Maintenance port was introduced in Version 2
            {
                return;
            }

            var maintanceReservation = new UrlReservation(AclMaintenanceUrl, new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));
            maintanceReservation.Create();
        }

        protected string DefaultDBPath()
        {
            var host = HostName == "*" ? "%" : HostName;
            var dbFolder = $"{host}-{Port}";
            if (!string.IsNullOrEmpty(VirtualDirectory))
            {
                dbFolder += $"-{FileUtils.SanitizeFolderName(VirtualDirectory)}";
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", BaseServiceName, dbFolder);
        }

        protected string DefaultLogPath()
        {
            // The default Logging folder in ServiceControl uses the env vae"%LocalApplicationData%".  Since this is env user specific we'll determine it based on profile path instead.
            // This only works for a user that has already logged in, which is fine for existing instances
            var userAccountName = UserAccount.ParseAccountName(Service.Account);

            var profilePath = userAccountName.RetrieveProfilePath();
            if (profilePath == null)
            {
                return null;
            }

            return Path.Combine(profilePath, $@"AppData\Local\Particular\{BaseServiceName}\logs");
        }

        public void RemoveUrlAcl()
        {
            foreach (var urlReservation in UrlReservation.GetAll().Where(p => p.Url.StartsWith(AclUrl, StringComparison.OrdinalIgnoreCase) ||
                                                                              p.Url.StartsWith(AclMaintenanceUrl, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    urlReservation.Delete();
                }
                catch
                {
                    ReportCard.Warnings.Add($"Failed to remove the URLACL for {Url} - Please remove manually via Netsh.exe");
                }
            }
        }

        /// <summary>
        /// Returns false if a reboot is required to complete deletion
        /// </summary>
        /// <returns></returns>
        public void RemoveBinFolder()
        {
            try
            {
                FileUtils.DeleteDirectory(InstallPath, true, false);
            }
            catch
            {
                ReportCard.Warnings.Add($"Could not delete the installation directory '{InstallPath}'. Please remove manually");
            }
        }

        public void RemoveLogsFolder()
        {
            try
            {
                FileUtils.DeleteDirectory(LogPath, true, false);
            }
            catch
            {
                ReportCard.Warnings.Add($"Could not delete the logs directory '{LogPath}'. Please remove manually");
            }
        }

        public void RemoveDataBaseFolder()
        {
            //Order by length descending in case they are nested paths
            var folders = GetDatabaseIndexes().ToList();

            foreach (var folder in folders.OrderByDescending(p => p.Length))
            {
                try
                {
                    FileUtils.DeleteDirectory(folder, true, false);
                }
                catch
                {
                    ReportCard.Warnings.Add($"Could not delete the RavenDB directory '{folder}'. Please remove manually");
                }
            }
        }

        public double GetDatabaseSizeInGb()
        {
            var folders = GetDatabaseIndexes().ToList();

            return folders.Sum(path => new DirectoryInfo(path).GetDirectorySize()) / (1024.0 * 1024 * 1024);
        }

        public void ApplyConfigChange()
        {
            var accountName = string.Equals(ServiceAccount, "LocalSystem", StringComparison.OrdinalIgnoreCase) ? "System" : ServiceAccount;
            var oldSettings = InstanceFinder.FindServiceControlInstance(Name);

            var fileSystemChanged = !string.Equals(oldSettings.LogPath, LogPath, StringComparison.OrdinalIgnoreCase);

            var queueNamesChanged = !(string.Equals(oldSettings.AuditQueue, AuditQueue, StringComparison.OrdinalIgnoreCase)
                                      && string.Equals(oldSettings.ErrorQueue, ErrorQueue, StringComparison.OrdinalIgnoreCase)
                                      && string.Equals(oldSettings.AuditLogQueue, AuditLogQueue, StringComparison.OrdinalIgnoreCase)
                                      && string.Equals(oldSettings.ErrorLogQueue, ErrorLogQueue, StringComparison.OrdinalIgnoreCase)
                                      && oldSettings.ForwardErrorMessages == ForwardErrorMessages
                                      && oldSettings.ForwardAuditMessages == ForwardAuditMessages);

            RecreateUrlAcl(oldSettings);

            if (fileSystemChanged)
            {
                var account = new NTAccount(accountName);
                var modifyAccessRule = new FileSystemAccessRule(account, FileSystemRights.Modify | FileSystemRights.Traverse | FileSystemRights.ListDirectory, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow);
                FileUtils.CreateDirectoryAndSetAcl(LogPath, modifyAccessRule);
            }

            Service.Description = Description;

            var configuration = ConfigurationManager.OpenExeConfiguration(Service.ExePath);
            var settings = configuration.AppSettings.Settings;
            
            ApplySettingsChanges(settings);

            configuration.ConnectionStrings.ConnectionStrings.Set("NServiceBus/Transport", ConnectionString);
            configuration.Save();

            var passwordSet = !string.IsNullOrWhiteSpace(ServiceAccountPwd);
            var accountChanged = !string.Equals(oldSettings.ServiceAccount, ServiceAccount, StringComparison.OrdinalIgnoreCase);
            var connectionStringChanged = !string.Equals(ConnectionString, oldSettings.ConnectionString, StringComparison.Ordinal);

            //have to save config prior to creating queues (if needed)
            if (queueNamesChanged || accountChanged || connectionStringChanged)
            {
                SetupInstance();
            }

            if (passwordSet || accountChanged)
            {
                Service.ChangeAccountDetails(accountName, ServiceAccountPwd);
            }
        }

        protected abstract void ApplySettingsChanges(KeyValueConfigurationCollection settings);

        protected abstract AppConfig CreateAppConfig();

        public virtual void EnableMaintenanceMode()
        {
            AppConfig = CreateAppConfig();
            AppConfig.EnableMaintenanceMode();
            InMaintenanceMode = true;
        }

        public virtual void DisableMaintenanceMode()
        {
            AppConfig = CreateAppConfig();
            AppConfig.DisableMaintenanceMode();
            InMaintenanceMode = false;
        }

        protected virtual IEnumerable<string> GetDatabaseIndexes()
        {
            return Enumerable.Empty<string>();
        }

        protected bool IsQueueDisabled(string queueName) => queueName != null && queueName.Equals("!disable", StringComparison.InvariantCultureIgnoreCase);

        protected virtual void ValidateConnectionString()
        {
        }

        protected virtual void ValidatePaths()
        {
        }

        protected virtual void ValidateQueueNames()
        {
        }

        protected virtual void ValidateServiceAccount()
        {
            var oldSettings = InstanceFinder.FindServiceControlInstance(Name);
            var passwordSet = !string.IsNullOrWhiteSpace(ServiceAccountPwd);
            var accountChanged = !string.Equals(oldSettings.ServiceAccount, ServiceAccount, StringComparison.OrdinalIgnoreCase);
            if (passwordSet || accountChanged)
            {
                try
                {
                    ServiceAccountValidation.Validate(this);
                }
                catch (IdentityNotMappedException)
                {
                    ReportCard.Errors.Add("The service account specified does not exist");
                    return;
                }
                catch (EngineValidationException ex)
                {
                    ReportCard.Errors.Add(ex.Message);
                    return;
                }
            }
        }

        public void RestoreAppConfig(string sourcePath)
        {
            if (sourcePath == null)
            {
                return;
            }

            var configFile = $"{Service.ExePath}.config";
            File.Copy(sourcePath, configFile, true);

            // Populate the config with common settings even if they are defaults
            // Will not clobber other settings in the config
            AppConfig = CreateAppConfig();
            AppConfig.Save();
        }

        public abstract void UpgradeFiles(string zipFilePath);

        public abstract void RunQueueCreation();

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

        public void ValidateChanges()
        {
            ValidatePaths();

            ValidateQueueNames();

            ValidateServiceAccount();

            ValidateConnectionString();
        }
        
        public void UpgradeTransportSeam()
        {
            //Legacy ASB Forwarding Topology should be migrated to the ASBS transport seam
            if (TransportPackage.Name == TransportNames.AzureServiceBusForwardingTopologyDeprecated)
            {
                TransportPackage = ServiceControlCoreTransports.Find(TransportNames.AzureServiceBus);

                AppConfig.SetTransportType(TransportPackage.TypeName);
                AppConfig.Save();
            }
        }

        public AppConfig AppConfig;

        public bool VersionHasServiceControlAuditFeatures => Version >= AuditFeatureMinVersion;

        static Version AuditFeatureMinVersion = new Version(4, 0);
    }
}