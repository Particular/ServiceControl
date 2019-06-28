namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using Configuration;
    using Configuration.ServiceControl;
    using FileSystem;
    using Queues;
    using Services;

    public class ServiceControlAuditInstance : ServiceControlBaseService, IServiceControlAuditInstance
    {
        protected override string BaseServiceName => "ServiceControl.Audit";

        public string ServiceControlQueueAddress { get; set; }

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
            settings.Set(AuditInstanceSettingsList.ForwardAuditMessages, ForwardAuditMessages.ToString());
            settings.Set(AuditInstanceSettingsList.AuditRetentionPeriod, AuditRetentionPeriod.ToString(), Version);
            settings.Set(AuditInstanceSettingsList.AuditQueue, AuditQueue);
        }

        protected override AppConfig CreateAppConfig()
        {
            return new ServiceControlAuditAppConfig(this);
        }

        protected override string GetTransportTypeSetting()
        {
            return AppConfig.Read(AuditInstanceSettingsList.TransportType, ServiceControlCoreTransports.All.Single(t => t.Default).TypeName).Trim();
        }

        public ServiceControlAuditInstance(WindowsServiceController service) : base(service)
        {
        }

        public override void Reload()
        {
            Service.Refresh();
            HostName = AppConfig.Read(AuditInstanceSettingsList.HostName, "localhost");
            Port = AppConfig.Read(AuditInstanceSettingsList.Port, 33333);
            DatabaseMaintenancePort = AppConfig.Read<int?>(AuditInstanceSettingsList.DatabaseMaintenancePort, null);
            LogPath = AppConfig.Read(AuditInstanceSettingsList.LogPath, DefaultLogPath());
            DBPath = AppConfig.Read(AuditInstanceSettingsList.DBPath, DefaultDBPath());
            AuditQueue = AppConfig.Read(AuditInstanceSettingsList.AuditQueue, "audit");
            AuditLogQueue = AppConfig.Read(AuditInstanceSettingsList.AuditLogQueue, $"{AuditQueue}.log");
            ForwardAuditMessages = AppConfig.Read(AuditInstanceSettingsList.ForwardAuditMessages, false);
            InMaintenanceMode = AppConfig.Read(AuditInstanceSettingsList.MaintenanceMode, false);
            ServiceControlQueueAddress = AppConfig.Read<string>(AuditInstanceSettingsList.ServiceControlQueueAddress, null);
            TransportPackage = DetermineTransportPackage();
            ConnectionString = ReadConnectionString();
            Description = GetDescription();
            ServiceAccount = Service.Account;

            if (TimeSpan.TryParse(AppConfig.Read(AuditInstanceSettingsList.AuditRetentionPeriod, (string)null), out var auditRetentionPeriod))
            {
                AuditRetentionPeriod = auditRetentionPeriod;
            }
        }

        public override void RunQueueCreation()
        {
            QueueCreation.RunQueueCreation(this);
        }

        public override void EnableMaintenanceMode()
        {
            base.AppConfig = CreateAppConfig();
            AppConfig.EnableMaintenanceMode();
            InMaintenanceMode = true;
        }

        public override void DisableMaintenanceMode()
        {
            base.AppConfig = CreateAppConfig();
            AppConfig.DisableMaintenanceMode();
            InMaintenanceMode = false;
        }

        public override void UpgradeFiles(string zipFilePath)
        {
            FileUtils.DeleteDirectory(InstallPath, true, true, "license", $"{Constants.ServiceControlAuditExe}.config");
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, BaseServiceName);
            FileUtils.UnzipToSubdirectory(zipFilePath, InstallPath, $@"Transports\{TransportPackage.ZipName}");
        }

        protected override IEnumerable<string> GetDatabaseIndexes()
        {
            return AppConfig.RavenDataPaths();
        }

        new ServiceControlAuditAppConfig AppConfig => (ServiceControlAuditAppConfig)base.AppConfig;
    }
}