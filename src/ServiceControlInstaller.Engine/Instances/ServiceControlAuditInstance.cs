namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using Configuration;
    using Configuration.ServiceControl;
    using FileSystem;
    using Queues;
    using ServiceControlInstaller.Engine.Validation;
    using Services;

    public class ServiceControlAuditInstance : ServiceControlBaseService, IServiceControlAuditInstance
    {
        public ServiceControlAuditInstance(IWindowsServiceController service) : base(service)
        {
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
            settings.Set(AuditInstanceSettingsList.ForwardAuditMessages, ForwardAuditMessages.ToString().ToLowerInvariant(), Version);
            settings.Set(AuditInstanceSettingsList.AuditRetentionPeriod, AuditRetentionPeriod.ToString(), Version);
            settings.Set(AuditInstanceSettingsList.AuditQueue, AuditQueue, Version);
            settings.Set(AuditInstanceSettingsList.AuditLogQueue, AuditLogQueue, Version);
            settings.Set(AuditInstanceSettingsList.EnableFullTextSearchOnBodies, EnableFullTextSearchOnBodies.ToString().ToLowerInvariant(), Version);
            settings.Set(AuditInstanceSettingsList.PersistenceType, PersistenceManifest.Name);
        }

        protected override AppConfig CreateAppConfig()
        {
            return new ServiceControlAuditAppConfig(this);
        }

        protected override TransportInfo DetermineTransportPackage()
        {
            var transportAppSetting = (AppConfig.Read<string>(AuditInstanceSettingsList.TransportType, null)?.Trim())
                ?? throw new Exception($"{AuditInstanceSettingsList.TransportType.Name} setting not found in app.config.");

            var transport = ServiceControlCoreTransports.Find(transportAppSetting);

            return transport ?? throw new Exception($"{AuditInstanceSettingsList.TransportType.Name} value of '{transportAppSetting}' in app.config is invalid.");
        }

        protected override string BaseServiceName => "ServiceControl.Audit";

        public override void Reload()
        {
            Service.Refresh();

            AppConfig = CreateAppConfig();

            InstanceName = AppConfig.Read(AuditInstanceSettingsList.InternalQueueName, Name);
            InstanceName = AppConfig.Read(AuditInstanceSettingsList.InstanceName, InstanceName);

            HostName = AppConfig.Read(AuditInstanceSettingsList.HostName, "localhost");
            Port = AppConfig.Read(AuditInstanceSettingsList.Port, 33333);
            DatabaseMaintenancePort = AppConfig.Read<int?>(AuditInstanceSettingsList.DatabaseMaintenancePort, null);
            LogPath = AppConfig.Read(AuditInstanceSettingsList.LogPath, DefaultLogPath());
            DBPath = AppConfig.Read(AuditInstanceSettingsList.DBPath, DefaultDBPath());
            AuditQueue = AppConfig.Read(AuditInstanceSettingsList.AuditQueue, "audit");
            AuditRetentionPeriod = TimeSpan.Parse(AppConfig.Read(AuditInstanceSettingsList.AuditRetentionPeriod, "30.00:00:00"));
            InMaintenanceMode = AppConfig.Read(AuditInstanceSettingsList.MaintenanceMode, false);
            ServiceControlQueueAddress = AppConfig.Read<string>(AuditInstanceSettingsList.ServiceControlQueueAddress, null);

            var persistenceType = AppConfig.Read<string>(AuditInstanceSettingsList.PersistenceType, null);
            PersistenceManifest = ServiceControlPersisters.GetAuditPersistence(persistenceType);

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

        protected override void Prepare(string zipResourceName, string destDir)
        {
            FileUtils.CloneDirectory(InstallPath, destDir, "license", $"{Constants.ServiceControlAuditExe}.config");
            FileUtils.UnzipToSubdirectory(zipResourceName, destDir);
            FileUtils.UnzipToSubdirectory("InstanceShared.zip", destDir);
            FileUtils.UnzipToSubdirectory("RavenDBServer.zip", Path.Combine(destDir, "Persisters", "RavenDB", "RavenDBServer"));
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

                var folderPath = settings[key].Value;
                yield return folderPath.StartsWith("~") //Raven uses ~ to indicate path is relative to bin folder e.g. ~/Data/Logs
                    ? Path.Combine(InstallPath, folderPath.Remove(0, 1))
                    : folderPath;
            }
        }

        protected override void ValidateConnectionString()
        {
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