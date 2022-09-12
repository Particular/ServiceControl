namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Instances;

    public class ServiceControlAppConfig : AppConfig
    {
        public ServiceControlAppConfig(IServiceControlInstance instance) : base(Path.Combine(instance.InstallPath, $"{Constants.ServiceControlExe}.config"))
        {
            details = instance;
        }

        protected override void UpdateSettings()
        {
            Config.ConnectionStrings.ConnectionStrings.Set("NServiceBus/Transport", details.ConnectionString);
            var settings = Config.AppSettings.Settings;
            var version = details.Version;
            settings.Set(ServiceControlSettings.VirtualDirectory, details.VirtualDirectory);
            settings.Set(ServiceControlSettings.Port, details.Port.ToString());
            settings.Set(ServiceControlSettings.DatabaseMaintenancePort, details.DatabaseMaintenancePort.ToString(), version);
            settings.Set(ServiceControlSettings.HostName, details.HostName);
            settings.Set(ServiceControlSettings.LogPath, details.LogPath);
            settings.Set(ServiceControlSettings.DBPath, details.DBPath);
            settings.Set(ServiceControlSettings.ForwardErrorMessages, details.ForwardErrorMessages.ToString(), version);
            settings.Set(ServiceControlSettings.TransportType, details.TransportPackage.TypeName, version);
            settings.Set(ServiceControlSettings.ErrorQueue, details.ErrorQueue);
            settings.Set(ServiceControlSettings.ErrorLogQueue, details.ForwardErrorMessages ? details.ErrorLogQueue : null);
            settings.Set(ServiceControlSettings.AuditRetentionPeriod, details.AuditRetentionPeriod.ToString(), version);
            settings.Set(ServiceControlSettings.ErrorRetentionPeriod, details.ErrorRetentionPeriod.ToString(), version);
            settings.Set(ServiceControlSettings.EnableFullTextSearchOnBodies, details.EnableFullTextSearchOnBodies.ToString(), version);
            settings.Set(ServiceControlSettings.RemoteInstances, RemoteInstanceConverter.ToJson(details.RemoteInstances), version);

            // Retired settings
            settings.RemoveIfRetired(ServiceControlSettings.AuditQueue, version);
            settings.RemoveIfRetired(ServiceControlSettings.AuditLogQueue, version);
            settings.RemoveIfRetired(ServiceControlSettings.ForwardAuditMessages, version);

            // Add Settings for performance tuning
            // See https://github.com/Particular/ServiceControl/issues/655
            if (!settings.AllKeys.Contains("Raven/Esent/MaxVerPages"))
            {
                settings.Add("Raven/Esent/MaxVerPages", "2048");
            }
        }

        public override void EnableMaintenanceMode()
        {
            var settings = Config.AppSettings.Settings;
            settings.Set(ServiceControlSettings.MaintenanceMode, bool.TrueString, details.Version);
            Config.Save();
        }

        public override void DisableMaintenanceMode()
        {
            var settings = Config.AppSettings.Settings;
            settings.Remove(ServiceControlSettings.MaintenanceMode.Name);
            Config.Save();
        }

        public override void SetTransportType(string transportTypeName)
        {
            var settings = Config.AppSettings.Settings;
            var version = details.Version;
            settings.Set(ServiceControlSettings.TransportType, transportTypeName, version);
        }

        public override IEnumerable<string> RavenDataPaths()
        {
            string[] keys =
            {
                "Raven/IndexStoragePath",
                "Raven/CompiledIndexCacheDirectory",
                "Raven/Esent/LogsPath",
                ServiceControlSettings.DBPath.Name
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
                    ? Path.Combine(details.InstallPath, folderpath.Remove(0, 1))
                    : folderpath;
            }
        }

        IServiceControlInstance details;
    }
}