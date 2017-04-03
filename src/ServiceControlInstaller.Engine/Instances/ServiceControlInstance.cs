// ReSharper disable MemberCanBePrivate.Global

namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.ObjectModel;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.ServiceProcess;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControlInstaller.Engine.Accounts;
    using ServiceControlInstaller.Engine.Configuration;
    using ServiceControlInstaller.Engine.FileSystem;
    using ServiceControlInstaller.Engine.Queues;
    using ServiceControlInstaller.Engine.ReportCard;
    using ServiceControlInstaller.Engine.Services;
    using ServiceControlInstaller.Engine.UrlAcl;
    using ServiceControlInstaller.Engine.Validation;
    using TimeoutException = System.ServiceProcess.TimeoutException;

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class ServiceControlInstance : IServiceControlInstance
    {
        public ServiceControlInstance(WindowsServiceController service)
        {
            Service = service;
            AppConfig = new ServiceControlAppConfig(this);
            ReadConfiguration();
        }

        public string InstallPath => Path.GetDirectoryName(Service.ExePath);

        public ReportCard ReportCard { get; set; }
        public WindowsServiceController Service { get; set; }
        public string LogPath { get; set; }
        public string DBPath { get; set; }
        public string HostName { get; set; }
        public int Port { get; set; }
        public string VirtualDirectory { get; set; }
        public string ErrorQueue { get; set; }
        public string AuditQueue { get; set; }
        public string ErrorLogQueue { get; set; }
        public string AuditLogQueue { get; set; }
        public bool ForwardAuditMessages { get; set; }
        public bool ForwardErrorMessages { get; set; }
        public string TransportPackage { get; set; }
        public string ConnectionString { get; set; }
        public string Description { get; set; }
        public string ServiceAccount { get; set; }
        public string ServiceAccountPwd { get; set; }
        public TimeSpan ErrorRetentionPeriod { get; set; }
        public TimeSpan AuditRetentionPeriod { get; set; }
        public bool InMaintenanceMode { get; set; }

        public string Name => Service.ServiceName;

        public ServiceControlAppConfig AppConfig;

        public void Reload()
        {
            ReadConfiguration();
        }

        public Version Version
        {
            get
            {
                // Service Can be registered but file deleted!
                if (File.Exists(Service.ExePath))
                {
                    var fileVersion = FileVersionInfo.GetVersionInfo(Service.ExePath);
                    return new Version(fileVersion.FileMajorPart, fileVersion.FileMinorPart, fileVersion.FileBuildPart);
                }
                return new Version(0, 0, 0);
            }
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
                if (string.IsNullOrWhiteSpace(VirtualDirectory))
                {
                    return $"http://{host}:{Port}/storage/";
                }
                return $"http://{host}:{Port}/{VirtualDirectory}{(VirtualDirectory.EndsWith("/") ? String.Empty : "/")}storage/";
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

        string GetDescription()
        {
            try
            {
                return Service.Description;
            }
            catch
            {
                return null;
            }
        }

        string DebuggerDisplay => $"{Name} - {Url} - {Version}";

        string DetermineTransportPackage()
        {
            var transportAppSetting = AppConfig.Read(SettingsList.TransportType, "NServiceBus.MsmqTransport").Split(",".ToCharArray())[0].Trim();
            var transport = Transports.All.FirstOrDefault(p => transportAppSetting.StartsWith(p.MatchOn , StringComparison.OrdinalIgnoreCase));
            if (transport != null)
            {
                return transport.Name;
            }
            return Transports.All.First(p => p.Default).Name;
        }

        public void ApplyConfigChange()
        {
            var accountName = string.Equals(ServiceAccount, "LocalSystem", StringComparison.OrdinalIgnoreCase) ? "System" : ServiceAccount;
            var oldSettings = FindByName(Name);
            
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
                QueueCreation.RunQueueCreation(this, accountName);
                try
                {
                    QueueCreation.RunQueueCreation(this);
                }
                catch (ServiceControlQueueCreationFailedException ex)
                {
                    ReportCard.Errors.Add(ex.Message);
                }
                catch (ServiceControlQueueCreationTimeoutException ex)
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
        }

        string DefaultDBPath()
        {
            var host = (HostName == "*") ? "%" : HostName;
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
                //TODO - Is null valid
                return null;
            }

            return Path.Combine(profilePath, @"AppData\Local\Particular\ServiceControl\logs");
        }

        public bool AppSettingExists(string key)
        {
            if (File.Exists(Service.ExePath))
            {
                var configManager = ConfigurationManager.OpenExeConfiguration(Service.ExePath);
                return configManager.AppSettings.Settings.AllKeys.Contains(key, StringComparer.OrdinalIgnoreCase);
            }
            return false;
        }

        public void RemoveUrlAcl()
        {
            foreach (var urlReservation in UrlReservation.GetAll().Where(p => p.Url.StartsWith(AclUrl, StringComparison.OrdinalIgnoreCase)))
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

        public bool TryStopService()
        {
            Service.Refresh();
            if (Service.Status == ServiceControllerStatus.Stopped)
            {
                return true;
            }

            Service.Stop();

            try
            {
                Service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
                var t = new Task(() =>
                {
                    while (!HasUnderlyingProcessExited())
                    {
                        Thread.Sleep(100);
                    }
                });
                t.Wait(5000);
            }
            catch (TimeoutException)
            {
                return false;
            }
            return true;
        }

        public bool TryStartService()
        {
            Service.Refresh();
            if (Service.Status == ServiceControllerStatus.Running)
            {
                return true;
            }

            Service.Start();

            try
            {
                Service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }
            catch (TimeoutException)
            {
                return false;
            }
            return true;
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

        public string BackupAppConfig()
        {
            var backupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControlInstaller", "ConfigBackups", FileUtils.SanitizeFolderName(Service.ServiceName));
            if (!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }
            var configFile = $"{Service.ExePath}.config";
            if (!File.Exists(configFile))
            {
                return null;
            }

            var destinationFile = Path.Combine(backupDirectory, $"{Guid.NewGuid():N}.config");
            File.Copy(configFile, destinationFile);
            return destinationFile;
        }

        public void RestoreAppConfig(string sourcePath)
        {
            if (sourcePath == null)
            {
                return;
            }
            var configFile = $"{Service.ExePath}.config";
            File.Copy(sourcePath, configFile, true);

            // Ensure Transport type is correct and populate the config with common settings even if they are defaults
            // Will not clobber other settings in the config 
            AppConfig = new ServiceControlAppConfig(this);
            AppConfig.Validate();
            AppConfig.Save();
        }

        public void UpgradeFiles(string zipFilePath)
        {
            FileUtils.DeleteDirectory(InstallPath, true, true, "license", "servicecontrol.exe.config");
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, "ServiceControl");
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, $@"Transports\{TransportPackage}");
        }

        public static ReadOnlyCollection<ServiceControlInstance> Instances()
        {
            var services = WindowsServiceController.FindInstancesByExe("ServiceControl.exe");
            return new ReadOnlyCollection<ServiceControlInstance>(services.Where(p => File.Exists(p.ExePath)).Select(p => new ServiceControlInstance(p)).ToList());
        }

        public static ServiceControlInstance FindByName(string instanceName)
        {
            try
            {
                return Instances().Single(p => p.Name.Equals(instanceName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                throw new Exception("Instance does not exists", ex);
            }
        }

        public void SetupInstance()
        {
            try
            {
                QueueCreation.RunQueueCreation(this);
            }
            catch (ServiceControlQueueCreationFailedException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }
            catch (ServiceControlQueueCreationTimeoutException ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }
        }

        bool HasUnderlyingProcessExited()
        {
            try
            {
                if (Service.ExePath != null)
                {
                    var process = Process.GetProcesses().FirstOrDefault(p => p.MainModule.FileName == Service.ExePath);
                    return (process == null);
                }
            }
            catch
            {
                //Service isn't accessible 
            }
            return true;
        }

        void ReadConfiguration()
        {
            Service.Refresh();
            HostName = AppConfig.Read(SettingsList.HostName, "localhost");
            Port = AppConfig.Read(SettingsList.Port, 33333);
            VirtualDirectory = AppConfig.Read(SettingsList.VirtualDirectory, (string) null);
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
            if (TimeSpan.TryParse(AppConfig.Read(SettingsList.ErrorRetentionPeriod, (string) null), out errorRetentionPeriod))
            {
                ErrorRetentionPeriod = errorRetentionPeriod;

            }
            TimeSpan auditRetentionPeriod;
            if (TimeSpan.TryParse(AppConfig.Read(SettingsList.AuditRetentionPeriod, (string) null), out auditRetentionPeriod))
            {
                AuditRetentionPeriod = auditRetentionPeriod;
            }
        }

        public void DisableMaintenanceMode()
        {
            AppConfig.DisableMaintenanceMode();
            InMaintenanceMode = false;
        }
        public void EnableMaintenanceMode()
        {
            AppConfig.EnableMaintenanceMode();
            InMaintenanceMode = true;
        }

        public void ValidateChanges()
        {
            try
            {
                PathsValidator.Validate(this);
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

            var oldSettings = FindByName(Name);
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
