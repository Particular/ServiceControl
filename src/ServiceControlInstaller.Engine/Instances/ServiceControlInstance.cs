// ReSharper disable MemberCanBePrivate.Global

namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using ServiceControlInstaller.Engine.Accounts;
    using ServiceControlInstaller.Engine.Configuration;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Database;
    using ServiceControlInstaller.Engine.FileSystem;
    using ServiceControlInstaller.Engine.Queues;
    using ServiceControlInstaller.Engine.ReportCard;
    using ServiceControlInstaller.Engine.Services;
    using ServiceControlInstaller.Engine.UrlAcl;
    using ServiceControlInstaller.Engine.Validation;

    public class ServiceControlInstance : BaseService, IServiceControlInstance
    {

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
        public string TransportPackage { get; set; }
        public string ConnectionString { get; set; }
        public TimeSpan ErrorRetentionPeriod { get; set; }
        public TimeSpan AuditRetentionPeriod { get; set; }
        public bool IsUpdatingDataStore { get; set; }
        public bool InMaintenanceMode { get; set; }
        public bool SkipQueueCreation { get; set; }
        public AppConfig AppConfig;
        public ReportCard ReportCard { get; set; }

        public ServiceControlInstance(WindowsServiceController service)
        {
            Service = service;
            AppConfig = new AppConfig(this);
            Reload();
        }

        public void Reload()
        {
            Service.Refresh();
            HostName = AppConfig.Read(SettingsList.HostName, "localhost");
            Port = AppConfig.Read(SettingsList.Port, 33333);
            DatabaseMaintenancePort = AppConfig.Read<int?>(SettingsList.DatabaseMaintenancePort, null);
            VirtualDirectory = AppConfig.Read(SettingsList.VirtualDirectory, (string)null);
            LogPath = AppConfig.Read(SettingsList.LogPath, DefaultLogPath());
            DBPath = AppConfig.Read(SettingsList.DBPath, DefaultDBPath());
            AuditQueue = AppConfig.Read(SettingsList.AuditQueue, "audit");
            AuditLogQueue = AppConfig.Read(SettingsList.AuditLogQueue, $"{AuditQueue}.log");
            ForwardAuditMessages = AppConfig.Read(SettingsList.ForwardAuditMessages, false);
            ForwardErrorMessages = AppConfig.Read(SettingsList.ForwardErrorMessages, false);
            InMaintenanceMode = AppConfig.Read(SettingsList.MaintenanceMode, false);
            ErrorQueue = AppConfig.Read(SettingsList.ErrorQueue, "error");
            ErrorLogQueue = AppConfig.Read(SettingsList.ErrorLogQueue, $"{ErrorQueue}.log");
            TransportPackage = DetermineTransportPackage();
            ConnectionString = ReadConnectionString();
            Description = GetDescription();
            ServiceAccount = Service.Account;

            TimeSpan errorRetentionPeriod;
            if (TimeSpan.TryParse(AppConfig.Read(SettingsList.ErrorRetentionPeriod, (string)null), out errorRetentionPeriod))
            {
                ErrorRetentionPeriod = errorRetentionPeriod;

            }
            TimeSpan auditRetentionPeriod;
            if (TimeSpan.TryParse(AppConfig.Read(SettingsList.AuditRetentionPeriod, (string)null), out auditRetentionPeriod))
            {
                AuditRetentionPeriod = auditRetentionPeriod;
            }

            UpdateDataMigrationMarker();
        }

        private void UpdateDataMigrationMarker()
        {
            IsUpdatingDataStore = File.Exists(Path.Combine(LogPath, "datamigration.marker"));
        }

        public override void RefreshServiceProperties()
        {
            base.RefreshServiceProperties();
            UpdateDataMigrationMarker();
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

        public string BrowsableUrl
        {
            get
            {
                string host;

                switch (HostName)
                {
                    case "*" :
                        host = "localhost";
                        break;
                    case "+" :
                          host = Environment.MachineName.ToLower();
                        break;
                    default :
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

        string ReadConnectionString()
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

        string DetermineTransportPackage()
        {
            var transportAppSetting = AppConfig.Read(SettingsList.TransportType, "NServiceBus.MsmqTransport").Split(",".ToCharArray())[0].Trim();
            var transport = V5Transports.All.FirstOrDefault(p => transportAppSetting.StartsWith(p.MatchOn , StringComparison.OrdinalIgnoreCase));
            if (transport != null)
            {
                return transport.Name;
            }
            return V5Transports.All.First(p => p.Default).Name;
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
                                      && oldSettings.ForwardAuditMessages == ForwardAuditMessages
                                      );


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
            var version = Version;
            settings.Set(SettingsList.HostName, HostName);
            settings.Set(SettingsList.Port, Port.ToString());
            
            if (oldSettings.Version.Major >= 2) //Maintenance port was introduced in Version 2
            {
                settings.Set(SettingsList.DatabaseMaintenancePort, DatabaseMaintenancePort.ToString());
            }
            
            settings.Set(SettingsList.LogPath, LogPath);
            settings.Set(SettingsList.ForwardAuditMessages, ForwardAuditMessages.ToString());
            settings.Set(SettingsList.ForwardErrorMessages, ForwardErrorMessages.ToString(), version);
            settings.Set(SettingsList.AuditRetentionPeriod, AuditRetentionPeriod.ToString(), version);
            settings.Set(SettingsList.ErrorRetentionPeriod, ErrorRetentionPeriod.ToString(), version);

            settings.RemoveIfRetired(SettingsList.HoursToKeepMessagesBeforeExpiring, version);

            settings.Set(SettingsList.AuditQueue, AuditQueue);
            settings.Set(SettingsList.ErrorQueue, ErrorQueue);

            if (Version >= Compatibility.ForwardingQueuesAreOptional.SupportedFrom)
            {
                if (!ForwardErrorMessages) ErrorLogQueue = null;
                if (!ForwardAuditMessages) AuditLogQueue = null;
            }
            settings.Set(SettingsList.ErrorLogQueue, ErrorLogQueue);
            settings.Set(SettingsList.AuditLogQueue, AuditLogQueue);

            configuration.ConnectionStrings.ConnectionStrings.Set("NServiceBus/Transport", ConnectionString);
            configuration.Save();

            var passwordSet = !string.IsNullOrWhiteSpace(ServiceAccountPwd);
            var accountChanged = !string.Equals(oldSettings.ServiceAccount, ServiceAccount, StringComparison.OrdinalIgnoreCase);
            var connectionStringChanged = !string.Equals(ConnectionString, oldSettings.ConnectionString, StringComparison.Ordinal);

            //have to save config prior to creating queues (if needed)

            if (queueNamesChanged || accountChanged || connectionStringChanged )
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

            if (passwordSet || accountChanged)
            {
                Service.ChangeAccountDetails(accountName, ServiceAccountPwd);
            }
        }

        private void RecreateUrlAcl(ServiceControlInstance oldSettings)
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

        string DefaultDBPath()
        {
            var host = HostName == "*" ? "%" : HostName;
            var dbFolder = $"{host}-{Port}";
            if (!string.IsNullOrEmpty(VirtualDirectory))
            {
                dbFolder += $"-{FileUtils.SanitizeFolderName(VirtualDirectory)}";
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControl", dbFolder);
        }

        string DefaultLogPath()
        {
            // The default Logging folder in ServiceControl uses the env vae"%LocalApplicationData%".  Since this is env user specific we'll determine it based on profile path instead.
            // This only works for a user that has already logged in, which is fine for existing instances
            var userAccountName = UserAccount.ParseAccountName(Service.Account);

            var profilePath = userAccountName.RetrieveProfilePath();
            if (profilePath == null)
            {
                return null;
            }

            return Path.Combine(profilePath, @"AppData\Local\Particular\ServiceControl\logs");
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
        ///     Returns false if a reboot is required to complete deletion
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
            var folders = AppConfig.RavenDataPaths().ToList();

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
            var folders = AppConfig.RavenDataPaths().ToList();

            return folders.Sum(path => new DirectoryInfo(path).GetDirectorySize()) / (1024.0 * 1024 * 1024);
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
            AppConfig = new AppConfig(this);
            AppConfig.Validate();
            AppConfig.Save();
        }

        public void UpgradeFiles(string zipFilePath)
        {
            FileUtils.DeleteDirectory(InstallPath, true, true, "license", $"{Constants.ServiceControlExe}.config");
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, "ServiceControl");
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, $@"Transports\{TransportPackage}");
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

        public void UpdateDatabase(Action<string> updateProgress)
        {
            try
            {
                DatabaseMigrations.RunDatabaseMigrations(this, updateProgress);
            }
            catch (DatabaseMigrationsException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }
        }

        public void RemoveDatabaseIndexes()
        {
            var folders = AppConfig.RavenDataPaths().ToList();

            foreach (var folder in folders.OrderByDescending(p => p.Length))
            {
                try
                {
                    FileUtils.DeleteDirectory(Path.Combine(folder, "Indexes"), true, false);
                }
                catch
                {
                    ReportCard.Warnings.Add($"Could not delete the RavenDB Indexes '{folder}'. This may cause problems in the data migration step. Please remove manually if errors occur.");
                }
            }
        }

        public void DisableMaintenanceMode()
        {
            AppConfig = new AppConfig(this);
            AppConfig.DisableMaintenanceMode();
            InMaintenanceMode = false;
        }

        public void EnableMaintenanceMode()
        {
            AppConfig = new AppConfig(this);
            AppConfig.EnableMaintenanceMode();
            InMaintenanceMode = true;
        }

        public void ValidateChanges()
        {
            try
            {
                new PathsValidator(this).RunValidation(false);
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
            try
            {
                ConnectionStringValidator.Validate(this);
            }
            catch (EngineValidationException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }
        }
    }
}
