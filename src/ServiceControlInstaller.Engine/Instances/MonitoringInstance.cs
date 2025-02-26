﻿namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Threading.Tasks;
    using Accounts;
    using Configuration;
    using Configuration.Monitoring;
    using FileSystem;
    using ReportCard;
    using Services;
    using Setup;
    using UrlAcl;
    using Validation;

    public class MonitoringInstance : BaseService, IMonitoringInstance
    {
        public MonitoringInstance(WindowsServiceController service)
        {
            Service = service;
            AppConfig = new AppConfig(this);
            Reload();
        }

        public AppConfig AppConfig { get; set; }

        public ReportCard ReportCard { get; set; }

        public int Port { get; set; }

        public string HostName { get; set; }

        public string ErrorQueue { get; set; }

        public string ConnectionString { get; set; }

        public string LogPath { get; set; }

        public bool SkipQueueCreation { get; set; }

        public string Url => $"http://{HostName}:{Port}/";

        public string BrowsableUrl
        {
            get
            {
                string host = HostName switch
                {
                    "*" => "localhost",
                    "+" => Environment.MachineName.ToLower(),
                    _ => HostName,
                };
                return $"http://{host}:{Port}/";
            }
        }

        public override void Reload()
        {
            Service.Refresh();

            AppConfig = new AppConfig(this);

            InstanceName = AppConfig.Read(SettingsList.InstanceName, Name);
            HostName = AppConfig.Read(SettingsList.HostName, "localhost");
            Port = AppConfig.Read(SettingsList.Port, 1234);
            LogPath = AppConfig.Read(SettingsList.LogPath, DefaultLogPath());
            ErrorQueue = AppConfig.Read(SettingsList.ErrorQueue, "error");
            TransportPackage = DetermineTransportPackage();
            ConnectionString = ReadConnectionString();
            Description = GetDescription();
            ServiceAccount = Service.Account;
        }

        string DefaultLogPath()
        {
            var userAccountName = UserAccount.ParseAccountName(Service.Account);
            var profilePath = userAccountName.RetrieveProfilePath();
            if (profilePath == null)
            {
                return null;
            }

            return Path.Combine(profilePath, $@"AppData\Local\Particular\{Name}\logs");
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

        TransportInfo DetermineTransportPackage()
        {
            var transportAppSetting = (AppConfig.Read<string>(SettingsList.TransportType, null)?.Trim())
                ?? throw new Exception($"{SettingsList.TransportType.Name} setting not found in app.config.");

            var transport = ServiceControlCoreTransports.Find(transportAppSetting);

            return transport ?? throw new Exception($"{SettingsList.TransportType.Name} value of '{transportAppSetting}' in app.config is invalid.");
        }

        public async Task ValidateChanges()
        {
            try
            {
                await new PathsValidator(this).RunValidation(false).ConfigureAwait(false);
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

            var queueNamesChanged = !string.Equals(oldSettings.ErrorQueue, ErrorQueue, StringComparison.OrdinalIgnoreCase);
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
                    InstanceSetup.Run(this);
                }
                catch (Exception ex)
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

        protected override void Prepare(string zipFilePath, string destDir)
        {
            FileUtils.CloneDirectory(InstallPath, destDir, "license", $"{Constants.MonitoringExe}.config");
            FileUtils.UnzipToSubdirectory(zipFilePath, destDir);
            FileUtils.UnzipToSubdirectory("InstanceShared.zip", destDir);
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
            AppConfig.Save();
        }

        public void SetupInstance()
        {
            try
            {
                InstanceSetup.Run(this);
            }
            catch (Exception ex)
            {
                ReportCard.Errors.Add(ex.Message);
            }
        }

        public void UpgradeTransportSeam()
        {
            TransportPackage = ServiceControlCoreTransports.UpgradedTransportSeam(TransportPackage);

            var config = new AppConfig(this);
            config.Save();
        }
    }
}
