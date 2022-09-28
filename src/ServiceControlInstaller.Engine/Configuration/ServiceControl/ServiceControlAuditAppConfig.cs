namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Instances;

    public class ServiceControlAuditAppConfig : AppConfig
    {
        public ServiceControlAuditAppConfig(IServiceControlAuditInstance instance) : base(Path.Combine(instance.InstallPath, $"{Constants.ServiceControlAuditExe}.config"))
        {
            this.instance = instance;
        }

        protected override void UpdateSettings()
        {
            Config.ConnectionStrings.ConnectionStrings.Set("NServiceBus/Transport", instance.ConnectionString);
            var settings = Config.AppSettings.Settings;
            var version = instance.Version;
            settings.Set(AuditInstanceSettingsList.Port, instance.Port.ToString());
            settings.Set(AuditInstanceSettingsList.HostName, instance.HostName);
            settings.Set(AuditInstanceSettingsList.LogPath, instance.LogPath);
            settings.Set(AuditInstanceSettingsList.ForwardAuditMessages, instance.ForwardAuditMessages.ToString());
            settings.Set(AuditInstanceSettingsList.TransportType, instance.TransportPackage.TypeName, version);
            settings.Set(AuditInstanceSettingsList.PersistenceType, instance.PersistenceManifest.TypeName);
            settings.Set(AuditInstanceSettingsList.AuditQueue, instance.AuditQueue);
            settings.Set(AuditInstanceSettingsList.AuditLogQueue, instance.ForwardAuditMessages ? instance.AuditLogQueue : null);
            settings.Set(AuditInstanceSettingsList.AuditRetentionPeriod, instance.AuditRetentionPeriod.ToString(), version);
            settings.Set(AuditInstanceSettingsList.ServiceControlQueueAddress, instance.ServiceControlQueueAddress);
            settings.Set(AuditInstanceSettingsList.EnableFullTextSearchOnBodies, instance.EnableFullTextSearchOnBodies.ToString(), version);

            foreach (var manifestSetting in instance.PersistenceManifest.Settings)
            {
                if (!settings.AllKeys.Contains(manifestSetting.Name))
                {
                    var value = manifestSetting.DefaultValue;

                    if (manifestSetting.Name == AuditInstanceSettingsList.DBPath.Name)
                    {
                        value = instance.DBPath;
                    }

                    if (manifestSetting.Name == AuditInstanceSettingsList.DatabaseMaintenancePort.Name)
                    {
                        value = instance.DatabaseMaintenancePort.ToString();
                    }

                    if (!string.IsNullOrEmpty(value))
                    {
                        settings.Add(manifestSetting.Name, value);
                    }
                }

                if (manifestSetting.Mandatory)
                {
                    if (!settings.AllKeys.Contains(manifestSetting.Name))
                    {
                        throw new Exception($"Mandatory setting {manifestSetting.Name} was not configured");
                    }

                    if (string.IsNullOrEmpty(settings[manifestSetting.Name].Value))
                    {
                        throw new Exception($"Mandatory setting {manifestSetting.Name} can not be null or empty");
                    }
                }
            }
        }

        public override void EnableMaintenanceMode()
        {
            var settings = Config.AppSettings.Settings;
            settings.Set(AuditInstanceSettingsList.MaintenanceMode, bool.TrueString, instance.Version);
            Config.Save();
        }

        public override void DisableMaintenanceMode()
        {
            var settings = Config.AppSettings.Settings;
            settings.Remove(AuditInstanceSettingsList.MaintenanceMode.Name);
            Config.Save();
        }

        public override void SetTransportType(string transportTypeName)
        {
            var settings = Config.AppSettings.Settings;
            var version = instance.Version;
            settings.Set(AuditInstanceSettingsList.TransportType, transportTypeName, version);
        }

        IServiceControlAuditInstance instance;
    }
}