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
            settings.Set(AuditInstanceSettingsList.DatabaseMaintenancePort, instance.DatabaseMaintenancePort.ToString(), version);
            settings.Set(AuditInstanceSettingsList.HostName, instance.HostName);
            settings.Set(AuditInstanceSettingsList.LogPath, instance.LogPath);
            settings.Set(AuditInstanceSettingsList.DBPath, instance.DBPath);
            settings.Set(AuditInstanceSettingsList.ForwardAuditMessages, instance.ForwardAuditMessages.ToString());
            settings.Set(AuditInstanceSettingsList.TransportType, instance.TransportPackage.TypeName, version);
            settings.Set(AuditInstanceSettingsList.PersistenceType, instance.PersistenceManifest.TypeName);
            settings.Set(AuditInstanceSettingsList.AuditQueue, instance.AuditQueue);
            settings.Set(AuditInstanceSettingsList.AuditLogQueue, instance.ForwardAuditMessages ? instance.AuditLogQueue : null);
            settings.Set(AuditInstanceSettingsList.AuditRetentionPeriod, instance.AuditRetentionPeriod.ToString(), version);
            settings.Set(AuditInstanceSettingsList.ServiceControlQueueAddress, instance.ServiceControlQueueAddress);
            settings.Set(AuditInstanceSettingsList.EnableFullTextSearchOnBodies, instance.EnableFullTextSearchOnBodies.ToString(), version);

            foreach (var setting in instance.PersistenceManifest.Settings)
            {
                if (!settings.AllKeys.Contains(setting.Key))
                {
                    settings.Add(setting.Key, setting.Value);
                }
            }

            //TODO: add the following to the RavenDB 3.5 manifest
            //settings.Add("Raven/Esent/MaxVerPages", "2048");
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

        public override IEnumerable<string> RavenDataPaths()
        {
            string[] keys =
            {
                "Raven/IndexStoragePath",
                "Raven/CompiledIndexCacheDirectory",
                "Raven/Esent/LogsPath",
                AuditInstanceSettingsList.DBPath.Name
            };

            var settings = Config.AppSettings.Settings;
            foreach (var key in keys)
            {
                if (!settings.AllKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var folderpath = settings[key].Value;
                yield return folderpath.StartsWith("~") //Raven uses ~ to indicate path is relative to bin folder e.g. ~/Data/Logs
                    ? Path.Combine(instance.InstallPath, folderpath.Remove(0, 1))
                    : folderpath;
            }
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