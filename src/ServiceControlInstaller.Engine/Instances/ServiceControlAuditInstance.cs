namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Configuration;
    using Configuration.ServiceControl;
    using FileSystem;
    using Queues;
    using Services;

    public class ServiceControlAuditInstance : ServiceControlBaseService, IServiceControlAuditInstance
    {
        public ServiceControlAuditInstance(IWindowsServiceController service) : this(service, Assembly.GetExecutingAssembly().Location)
        {
        }

        public ServiceControlAuditInstance(IWindowsServiceController service, string deploymentCachePath) : base(service)
        {
            this.deploymentCachePath = deploymentCachePath;

            Reload();
        }

        public TimeSpan AuditRetentionPeriod { get; set; }
        public string ServiceControlQueueAddress { get; set; }
        public PersistenceManifest PersistenceManifest { get; set; }

        protected override void ApplySettingsChanges(KeyValueConfigurationCollection settings)
        {
            if (!ForwardErrorMessages)
            {
                ErrorLogQueue = null;
            }

            if (!ForwardAuditMessages)
            {
                AuditLogQueue = null;
            }

            settings.Set(AuditInstanceSettingsList.HostName, HostName);
            settings.Set(AuditInstanceSettingsList.Port, Port.ToString());
            settings.Set(AuditInstanceSettingsList.DatabaseMaintenancePort, DatabaseMaintenancePort.ToString(), Version);
            settings.Set(AuditInstanceSettingsList.LogPath, LogPath);
            settings.Set(AuditInstanceSettingsList.ForwardAuditMessages, ForwardAuditMessages.ToString(), Version);
            settings.Set(AuditInstanceSettingsList.AuditRetentionPeriod, AuditRetentionPeriod.ToString(), Version);
            settings.Set(AuditInstanceSettingsList.AuditQueue, AuditQueue, Version);
            settings.Set(AuditInstanceSettingsList.AuditLogQueue, AuditLogQueue, Version);
            settings.Set(AuditInstanceSettingsList.EnableFullTextSearchOnBodies, EnableFullTextSearchOnBodies.ToString(), Version);
            settings.Set(AuditInstanceSettingsList.PersistenceType, PersistenceManifest.TypeName);
        }

        protected override AppConfig CreateAppConfig()
        {
            return new ServiceControlAuditAppConfig(this);
        }

        protected override string GetTransportTypeSetting()
        {
            return AppConfig.Read(AuditInstanceSettingsList.TransportType, ServiceControlCoreTransports.All.Single(t => t.Default).TypeName).Trim();
        }

        protected override string BaseServiceName => "ServiceControl.Audit";

        public override void Reload()
        {
            Service.Refresh();

            AppConfig = CreateAppConfig();
            HostName = AppConfig.Read(AuditInstanceSettingsList.HostName, "localhost");
            Port = AppConfig.Read(AuditInstanceSettingsList.Port, 33333);
            DatabaseMaintenancePort = AppConfig.Read<int?>(AuditInstanceSettingsList.DatabaseMaintenancePort, null);
            LogPath = AppConfig.Read(AuditInstanceSettingsList.LogPath, DefaultLogPath());
            DBPath = AppConfig.Read(AuditInstanceSettingsList.DBPath, DefaultDBPath());
            AuditQueue = AppConfig.Read(AuditInstanceSettingsList.AuditQueue, "audit");
            AuditRetentionPeriod = TimeSpan.Parse(AppConfig.Read(AuditInstanceSettingsList.AuditRetentionPeriod, "30.00:00:00"));
            InMaintenanceMode = AppConfig.Read(AuditInstanceSettingsList.MaintenanceMode, false);
            ServiceControlQueueAddress = AppConfig.Read<string>(AuditInstanceSettingsList.ServiceControlQueueAddress, null);

            var zipInfo = ServiceControlAuditZipInfo.Find(deploymentCachePath);

            var manifests = ServiceControlAuditPersisters.LoadAllManifests(zipInfo.FilePath);

            var persistenceType = AppConfig.Read<string>(AuditInstanceSettingsList.PersistenceType, null);

            if (string.IsNullOrEmpty(persistenceType))
            {
                PersistenceManifest = manifests.Single(m => m.Name == "RavenDb35");
            }
            else
            {
                PersistenceManifest = manifests.Single(m => m.TypeName == persistenceType);
            }

            TransportPackage = DetermineTransportPackage();
            ConnectionString = ReadConnectionString();
            Description = GetDescription();
            ServiceAccount = Service.Account;

            ForwardAuditMessages = AppConfig.Read(AuditInstanceSettingsList.ForwardAuditMessages, false);
            if (ForwardAuditMessages)
            {
                AuditLogQueue = AppConfig.Read(AuditInstanceSettingsList.AuditLogQueue, $"{AuditQueue}.log");
            }

            EnableFullTextSearchOnBodies = AppConfig.Read(AuditInstanceSettingsList.EnableFullTextSearchOnBodies, true);
        }

        public override void RunQueueCreation()
        {
            QueueCreation.RunQueueCreation(this);
        }
        public override void UpgradeFiles(string zipFilePath)
        {
            FileUtils.DeleteDirectory(InstallPath, true, true, "license", $"{Constants.ServiceControlAuditExe}.config");
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, BaseServiceName);
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, $@"Transports\{TransportPackage.ZipName}");
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, $@"Persisters\{PersistenceManifest.Name}");
        }

        protected override IEnumerable<string> GetPersistencePathsToCleanUp()
        {
            var settings = AppConfig.Config.AppSettings.Settings;
            foreach (var key in PersistenceManifest.SettingsWithPathsToCleanup)
            {
                if (!settings.AllKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var folderpath = settings[key].Value;
                yield return folderpath.StartsWith("~") //Raven uses ~ to indicate path is relative to bin folder e.g. ~/Data/Logs
                    ? Path.Combine(InstallPath, folderpath.Remove(0, 1))
                    : folderpath;
            }
        }

        readonly string deploymentCachePath;
    }
}