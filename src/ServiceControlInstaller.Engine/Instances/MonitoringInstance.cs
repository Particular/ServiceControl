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
    using ServiceControlInstaller.Engine.FileSystem;
    using ServiceControlInstaller.Engine.Queues;
    using ServiceControlInstaller.Engine.ReportCard;
    using ServiceControlInstaller.Engine.Services;
    using ServiceControlInstaller.Engine.UrlAcl;
    using ServiceControlInstaller.Engine.Validation;
    using AppConfig = ServiceControlInstaller.Engine.Configuration.Monitoring.AppConfig;
    using SettingsList = ServiceControlInstaller.Engine.Configuration.Monitoring.SettingsList;

    public class MonitoringInstance : BaseService, IMonitoringInstance
    {
        public int Port { get; set; }
        public string HostName { get; set; }
        public string TransportPackage { get; set; }
        public string ErrorQueue { get; set; }
        public string ConnectionString { get; set; }
        public string LogPath { get; set; }
        public AppConfig AppConfig { get; set; }
        public ReportCard ReportCard { get; set; }
        public bool SkipQueueCreation { get; set; }

        public MonitoringInstance(WindowsServiceController service)
        {
            Service = service;
            AppConfig = new AppConfig(this);
            Reload();
        }

        public void Reload()
        {
            Service.Refresh();
            HostName = AppConfig.Read(SettingsList.HostName, "localhost");
            Port = AppConfig.Read(SettingsList.Port, 1234);
            LogPath = AppConfig.Read(SettingsList.LogPath, DefaultLogPath());
            ErrorQueue = AppConfig.Read(SettingsList.ErrorQueue, "error");
            TransportPackage = DetermineTransportPackage();
            ConnectionString = ReadConnectionString();
            Description = GetDescription();
            ServiceAccount = Service.Account;
        }

        public string Url => $"http://{HostName}:{Port}/";

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
                return $"http://{host}:{Port}/";
            }
        }


        string DefaultLogPath()
        {
            var userAccountName = UserAccount.ParseAccountName(Service.Account);
            var profilePath = userAccountName.RetrieveProfilePath();
            if (profilePath == null)
            {
                return null;
            }
            return Path.Combine(profilePath,$@"AppData\Local\Particular\{Name}\logs");
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
            var transport = V6Transports.All.FirstOrDefault(p => transportAppSetting.StartsWith(p.MatchOn, StringComparison.OrdinalIgnoreCase));
            if (transport != null)
            {
                return transport.Name;
            }
            return V6Transports.All.First(p => p.Default).Name;
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
            
            var oldSettings = InstanceFinder.FindMonitoringInstance(Name);
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
       
        public void ApplyConfigChange()
        {
            var accountName = string.Equals(ServiceAccount, "LocalSystem", StringComparison.OrdinalIgnoreCase) ? "System" : ServiceAccount;
            var oldSettings = InstanceFinder.FindMonitoringInstance(Name);
            var fileSystemChanged = !string.Equals(oldSettings.LogPath, LogPath, StringComparison.OrdinalIgnoreCase);

            var queueNamesChanged = !(string.Equals(oldSettings.ErrorQueue, ErrorQueue, StringComparison.OrdinalIgnoreCase));
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
            settings.Set(SettingsList.HostName, HostName);
            settings.Set(SettingsList.Port, Port.ToString());
            settings.Set(SettingsList.LogPath, LogPath);
            settings.Set(SettingsList.ErrorQueue, ErrorQueue);
            configuration.ConnectionStrings.ConnectionStrings.Set("NServiceBus/Transport", ConnectionString);
            configuration.Save();
            var passwordSet = !string.IsNullOrWhiteSpace(ServiceAccountPwd);
            var accountChanged = !string.Equals(oldSettings.ServiceAccount, ServiceAccount, StringComparison.OrdinalIgnoreCase);
            var connectionStringChanged = !string.Equals(ConnectionString, oldSettings.ConnectionString, StringComparison.Ordinal);

            //have to save config prior to creating queues (if needed)
            if (queueNamesChanged || accountChanged || connectionStringChanged)
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

        void RecreateUrlAcl(MonitoringInstance oldSettings)
        {
            oldSettings.RemoveUrlAcl();
            var reservation = new UrlReservation(Url, new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));
            reservation.Create();
        }
        
        public void RemoveUrlAcl()
        {
            foreach (var urlReservation in UrlReservation.GetAll().Where(p => p.Url.StartsWith(Url, StringComparison.OrdinalIgnoreCase)))
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

        public void UpgradeFiles(string zipFilePath)
        {
            FileUtils.DeleteDirectory(InstallPath, true, true, "license", $"{Constants.MonitoringExe}.config");
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, "ServiceControl.Monitoring");
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, $@"Transports\{TransportPackage}");
        }
        
        public void RestoreAppConfig(string sourcePath)
        {
            if (sourcePath == null)
            {
                return;
            }
            File.Copy(sourcePath, $"{Service.ExePath}.config", true);

            // Populate the config with common settings even if they are defaults
            // Will not clobber other settings in the config 
            AppConfig = new AppConfig(this);
            AppConfig.Validate();
            AppConfig.Save();
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
    }
}
