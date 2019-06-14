namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using Instances;

    public class ServiceControlAppConfig : AppConfig
    {
        IServiceControlInstance details;

        public ServiceControlAppConfig(IServiceControlInstance instance) : base(Path.Combine(instance.InstallPath, $"{Constants.ServiceControlExe}.config"))
        {
            details = instance;
        }

        protected override void UpdateSettings()
        {
            Config.ConnectionStrings.ConnectionStrings.Set("NServiceBus/Transport", details.ConnectionString);
            var settings = Config.AppSettings.Settings;
            var version = details.Version;
            settings.Set(SettingsList.VirtualDirectory, details.VirtualDirectory);
            settings.Set(SettingsList.Port, details.Port.ToString());
            settings.Set(SettingsList.DatabaseMaintenancePort, details.DatabaseMaintenancePort.ToString(), version);
            settings.Set(SettingsList.HostName, details.HostName);
            settings.Set(SettingsList.LogPath, details.LogPath);
            settings.Set(SettingsList.DBPath, details.DBPath);
            settings.Set(SettingsList.ForwardErrorMessages, details.ForwardErrorMessages.ToString(), version);
            settings.Set(SettingsList.TransportType, details.TransportPackage.TypeName, version);
            settings.Set(SettingsList.ErrorQueue, details.ErrorQueue);
            settings.Set(SettingsList.ErrorLogQueue, details.ErrorLogQueue);
            settings.Set(SettingsList.ErrorRetentionPeriod, details.ErrorRetentionPeriod.ToString(), version);

            // Add Settings for performance tuning
            // See https://github.com/Particular/ServiceControl/issues/655
            if (!settings.AllKeys.Contains("Raven/Esent/MaxVerPages"))
            {
                settings.Add("Raven/Esent/MaxVerPages", "2048");
            }
        }

        public void EnableMaintenanceMode()
        {
            var settings = Config.AppSettings.Settings;
            settings.Set(SettingsList.MaintenanceMode, Boolean.TrueString, details.Version);
            Config.Save();
        }

        public void DisableMaintenanceMode()
        {
            var settings = Config.AppSettings.Settings;
            settings.Remove(SettingsList.MaintenanceMode.Name);
            Config.Save();
        }

        public IEnumerable<string> RavenDataPaths()
        {
            string[] keys =
            {
                "Raven/IndexStoragePath",
                "Raven/CompiledIndexCacheDirectory",
                "Raven/Esent/LogsPath",
                SettingsList.DBPath.Name
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
    }

    public class ServiceControlAuditAppConfig : AppConfig
    {
        IServiceControlAuditInstance details;

        public ServiceControlAuditAppConfig(IServiceControlAuditInstance instance) : base(Path.Combine(instance.InstallPath, $"{Constants.ServiceControlAuditExe}.config"))
        {
            details = instance;
        }

        protected override void UpdateSettings()
        {
            Config.ConnectionStrings.ConnectionStrings.Set("NServiceBus/Transport", details.ConnectionString);
            var settings = Config.AppSettings.Settings;
            var version = details.Version;
            settings.Set(AuditInstanceSettingsList.Port, details.Port.ToString());
            settings.Set(AuditInstanceSettingsList.DatabaseMaintenancePort, details.DatabaseMaintenancePort.ToString(), version);
            settings.Set(AuditInstanceSettingsList.HostName, details.HostName);
            settings.Set(AuditInstanceSettingsList.LogPath, details.LogPath);
            settings.Set(AuditInstanceSettingsList.DBPath, details.DBPath);
            settings.Set(AuditInstanceSettingsList.ForwardAuditMessages, details.ForwardAuditMessages.ToString());
            settings.Set(AuditInstanceSettingsList.TransportType, details.TransportPackage.TypeName, version);
            settings.Set(AuditInstanceSettingsList.AuditQueue, details.AuditQueue);
            settings.Set(AuditInstanceSettingsList.AuditLogQueue, details.AuditLogQueue);
            settings.Set(AuditInstanceSettingsList.AuditRetentionPeriod, details.AuditRetentionPeriod.ToString(), version);

            // Add Settings for performance tuning
            // See https://github.com/Particular/ServiceControl/issues/655
            if (!settings.AllKeys.Contains("Raven/Esent/MaxVerPages"))
            {
                settings.Add("Raven/Esent/MaxVerPages", "2048");
            }
        }

        public void EnableMaintenanceMode()
        {
            var settings = Config.AppSettings.Settings;
            settings.Set(AuditInstanceSettingsList.MaintenanceMode, Boolean.TrueString, details.Version);
            Config.Save();
        }

        public void DisableMaintenanceMode()
        {
            var settings = Config.AppSettings.Settings;
            settings.Remove(AuditInstanceSettingsList.MaintenanceMode.Name);
            Config.Save();
        }

        public IEnumerable<string> RavenDataPaths()
        {
            string[] keys =
            {
                "Raven/IndexStoragePath",
                "Raven/CompiledIndexCacheDirectory",
                "Raven/Esent/LogsPath",
                SettingsList.DBPath.Name
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
    }

    public abstract class AppConfig : AppConfigWrapper
    {
        protected AppConfig(string configFilePath) : base(configFilePath)
        {
        }

        public void Save()
        {
            UpdateSettings();

            UpdateRuntimeSection();

            Config.Save();
        }

        void UpdateRuntimeSection()
        {
            var runtimesection = Config.GetSection("runtime");
            var runtimeXml = XDocument.Parse(runtimesection.SectionInformation.GetRawXml() ?? "<runtime/>");

            // Set gcServer Value if it does not exist
            var gcServer = runtimeXml.Descendants("gcServer").SingleOrDefault();
            if (gcServer == null) //So no config so we can set
            {
                gcServer = new XElement("gcServer");
                gcServer.SetAttributeValue("enabled", "true");
                if (runtimeXml.Root != null)
                {
                    runtimeXml.Root.Add(gcServer);
                    runtimesection.SectionInformation.SetRawXml(runtimeXml.Root.ToString());
                }
            }
        }

        protected abstract void UpdateSettings();
    }
}