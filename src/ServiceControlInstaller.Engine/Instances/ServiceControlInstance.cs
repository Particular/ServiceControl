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
            ReadConfiguration();
        }

        public string InstallPath
        {
            get { return Path.GetDirectoryName(Service.ExePath); }
        }
      
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

        public string Name
        {
            get { return Service.ServiceName; }
        }

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
                var baseUrl = string.Format("http://{0}:{1}/api/", HostName, Port);
                if (string.IsNullOrWhiteSpace(VirtualDirectory))
                {
                    return baseUrl;
                }
                return string.Format("{0}{1}{2}api/", baseUrl, VirtualDirectory, VirtualDirectory.EndsWith("/") ? "" : "/");
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

                var baseUrl = string.Format("http://{0}:{1}/api/", host, Port);
                if (string.IsNullOrWhiteSpace(VirtualDirectory))
                {
                    return baseUrl;
                }
                return string.Format("{0}{1}{2}api/", baseUrl, VirtualDirectory, VirtualDirectory.EndsWith("/") ? "" : "/");
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

        string DebuggerDisplay
        {
            get { return string.Format("{0} - {1} - {2}", Name, Url, Version); }
        }

        string DetermineTransportPackage()
        {
            var transportAppSetting = ReadAppSetting(SettingsList.TransportType, "NServiceBus.MsmqTransport").Split(",".ToCharArray())[0].Trim();
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
            var urlaclChanged = !(oldSettings.Port == Port
                                  && string.Equals(oldSettings.Name, Name, StringComparison.OrdinalIgnoreCase)
                                  && string.Equals(oldSettings.VirtualDirectory, VirtualDirectory, StringComparison.OrdinalIgnoreCase));

            var fileSystemChanged = !string.Equals(oldSettings.LogPath, LogPath, StringComparison.OrdinalIgnoreCase);

            var queueNamesChanged = !(string.Equals(oldSettings.AuditQueue, AuditQueue, StringComparison.OrdinalIgnoreCase)
                                      && string.Equals(oldSettings.ErrorQueue, ErrorQueue, StringComparison.OrdinalIgnoreCase)
                                      && string.Equals(oldSettings.AuditLogQueue, AuditLogQueue, StringComparison.OrdinalIgnoreCase)
                                      && string.Equals(oldSettings.ErrorLogQueue, ErrorLogQueue, StringComparison.OrdinalIgnoreCase));
            if (urlaclChanged)
            {
                oldSettings.RemoveUrlAcl();
                var reservation = new UrlReservation(Url, new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));
                reservation.Create();
            }

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

        string DefaultDBPath()
        {
            var host = (HostName == "*") ? "%" : HostName;
            var dbFolder = string.Format("{0}-{1}", host, Port);
            if (!string.IsNullOrEmpty(VirtualDirectory))
            {
                dbFolder += String.Format("-{0}", FileUtils.SanitizeFolderName(VirtualDirectory));
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

        T ReadAppSetting<T>(SettingInfo keyInfo, T defaultValue)
        {
            return ReadAppSetting(keyInfo.Name, defaultValue);
        }

        public T ReadAppSetting<T>(string key, T defaultValue)
        {
            if (File.Exists(Service.ExePath))
            {
                var configManager = ConfigurationManager.OpenExeConfiguration(Service.ExePath);
                if (configManager.AppSettings.Settings.AllKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    return (T) Convert.ChangeType(configManager.AppSettings.Settings[key].Value, typeof(T));
                }
            }
            try
            {
                var parts = key.Split("/".ToCharArray(), 2);
                return RegistryReader<T>.Read(parts[0], parts[1], defaultValue);
            }
            catch (Exception)
            {
                // Fall through to default
            }

            return defaultValue;
        }

        public void RemoveUrlAcl()
        {
            foreach (var urlReservation in UrlReservation.GetAll().Where(p => p.Url.Equals(Url, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    urlReservation.Delete();
                }
                catch
                {
                    ReportCard.Warnings.Add(string.Format("Failed to remove the URLACL for {0} - Please remove manually via Netsh.exe", Url));
                }
            }
        }

        public bool TryStopService()
        {
            Service.Refresh();
            if (Service.Status != ServiceControllerStatus.Stopped)
            {
                Service.Stop();
            }

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
            if (Service.Status != ServiceControllerStatus.Running)
            {
                Service.Start();
            }

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
                ReportCard.Warnings.Add(string.Format("Could not delete the logs directory '{0}'. Please remove manually", LogPath));
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
                ReportCard.Warnings.Add(string.Format("Could not delete the logs directory '{0}'. Please remove manually", LogPath));
            }
        }

        public void RemoveDataBaseFolder()
        {
            try
            {
                FileUtils.DeleteDirectory(DBPath, true, false);
            }
            catch
            {
                ReportCard.Warnings.Add(string.Format("Could not delete the database directory '{0}'. Please remove manually", DBPath));
            }
        }

        public string BackupAppConfig()
        {
            var backupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControlInstaller", "ConfigBackups", FileUtils.SanitizeFolderName(Service.ServiceName));
            if (!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }
            var configFile = string.Format("{0}.config", Service.ExePath);
            if (!File.Exists(configFile))
            {
                return null;
            }

            var destinationFile = Path.Combine(backupDirectory, string.Format("{0:N}.config", Guid.NewGuid()));
            File.Copy(configFile, destinationFile);
            return destinationFile;
        }

        public void RestoreAppConfig(string sourcePath)
        {
            if (sourcePath == null)
            {
                return;
            }
            var configFile = string.Format("{0}.config", Service.ExePath);
            File.Copy(sourcePath, configFile, true);

            // Ensure Transport type is correct and populate the config with common settings even if they are defaults
            // Will not clobber other settings in the config 
            var configWriter = new ConfigurationWriter(this);
            configWriter.Validate();
            configWriter.Save();
        }

        public void UpgradeFiles(string zipFilePath)
        {
            FileUtils.DeleteDirectory(InstallPath, true, true, "license", "servicecontrol.exe.config");
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, "ServiceControl");
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, string.Format(@"Transports\{0}", TransportPackage));
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

        public static void CheckIfServiceNameTaken(string instanceName)
        {
            if (ServiceController.GetServices().SingleOrDefault(p => p.ServiceName.Equals(instanceName, StringComparison.OrdinalIgnoreCase)) != null)
                throw new Exception(string.Format("Invalid Service Name. There is already a windows service called '{0}'", instanceName));
        }

        public void RunInstanceToCreateQueues()
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
            HostName = ReadAppSetting(SettingsList.HostName, "localhost");
            Port = ReadAppSetting(SettingsList.Port, 33333);
            VirtualDirectory = ReadAppSetting(SettingsList.VirtualDirectory, (string) null);
            LogPath = ReadAppSetting(SettingsList.LogPath, DefaultLogPath());
            DBPath = ReadAppSetting(SettingsList.DBPath, DefaultDBPath());
            AuditQueue = ReadAppSetting(SettingsList.AuditQueue, "audit");
            AuditLogQueue = ReadAppSetting(SettingsList.AuditLogQueue, string.Format("{0}.log", AuditQueue));
            ForwardAuditMessages = ReadAppSetting(SettingsList.ForwardAuditMessages, false);
            ForwardErrorMessages = ReadAppSetting(SettingsList.ForwardErrorMessages, false);
            ErrorQueue = ReadAppSetting(SettingsList.ErrorQueue, "error");
            ErrorLogQueue = ReadAppSetting(SettingsList.ErrorLogQueue, string.Format("{0}.log", ErrorQueue));
            TransportPackage = DetermineTransportPackage();
            ConnectionString = ReadConnectionString();
            Description = GetDescription();
            ServiceAccount = Service.Account;

            TimeSpan errorRetentionPeriod;
            if (TimeSpan.TryParse(ReadAppSetting(SettingsList.ErrorRetentionPeriod, (string) null), out errorRetentionPeriod))
            {
                ErrorRetentionPeriod = errorRetentionPeriod;

            }
            TimeSpan auditRetentionPeriod;
            if (TimeSpan.TryParse(ReadAppSetting(SettingsList.AuditRetentionPeriod, (string) null), out auditRetentionPeriod))
            {
                AuditRetentionPeriod = auditRetentionPeriod;
            }

            

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
