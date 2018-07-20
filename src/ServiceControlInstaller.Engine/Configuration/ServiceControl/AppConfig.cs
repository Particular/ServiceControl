namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using ServiceControlInstaller.Engine.Instances;

    public class AppConfig : AppConfigWrapper
    {
        IServiceControlInstance details;
        
        public AppConfig(IServiceControlInstance details) : base(Path.Combine(details.InstallPath, $"{Constants.ServiceControlExe}.config"))
        {
            this.details = details;
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

        public void Save()
        {
            Config.ConnectionStrings.ConnectionStrings.Set("NServiceBus/Transport", details.ConnectionString);
            var settings = Config.AppSettings.Settings;
            var version = details.Version;
            settings.Set(SettingsList.VirtualDirectory, details.VirtualDirectory);
            settings.Set(SettingsList.Port, details.Port.ToString());
            settings.Set(SettingsList.DatabaseMaintenancePort, details.DatabaseMaintenancePort.ToString());
            settings.Set(SettingsList.HostName, details.HostName);
            settings.Set(SettingsList.LogPath, details.LogPath);
            settings.Set(SettingsList.DBPath, details.DBPath);
            settings.Set(SettingsList.ForwardAuditMessages, details.ForwardAuditMessages.ToString());
            settings.Set(SettingsList.ForwardErrorMessages, details.ForwardErrorMessages.ToString(), version);
            settings.Set(SettingsList.TransportType, details.TransportPackage.TypeName, version);
            settings.Set(SettingsList.AuditQueue, details.AuditQueue);
            settings.Set(SettingsList.ErrorQueue, details.ErrorQueue);
            settings.Set(SettingsList.ErrorLogQueue, details.ErrorLogQueue);
            settings.Set(SettingsList.AuditLogQueue, details.AuditLogQueue);
            settings.Set(SettingsList.AuditRetentionPeriod, details.AuditRetentionPeriod.ToString(), version);
            settings.Set(SettingsList.ErrorRetentionPeriod, details.ErrorRetentionPeriod.ToString(), version);

            // Add Settings for performance tuning
            // See https://github.com/Particular/ServiceControl/issues/655
            if (!settings.AllKeys.Contains("Raven/Esent/MaxVerPages"))
            {
                settings.Add("Raven/Esent/MaxVerPages", "2048");
            }
            UpdateRuntimeSection();

            Config.Save();
        }

        void UpdateRuntimeSection()
        {

            var runtimesection = Config.GetSection("runtime");
            var runtimeXml = XDocument.Parse(runtimesection.SectionInformation.GetRawXml() ?? "<runtime/>");

            // Set gcServer Value if it does not exist
            var gcServer = runtimeXml.Descendants("gcServer").SingleOrDefault();
            if (gcServer == null)  //So no config so we can set
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

        public IEnumerable<string> RavenDataPaths()
        {
            string[] keys = {
                 "Raven/IndexStoragePath"
                ,"Raven/CompiledIndexCacheDirectory"
                ,"Raven/Esent/LogsPath",
                SettingsList.DBPath.Name
            };

            var settings = Config.AppSettings.Settings;
            foreach (var key in keys)
            {
                if (!settings.AllKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                    continue;
                var folderpath = settings[key].Value;
                yield return folderpath.StartsWith("~")    //Raven uses ~ to indicate path is relative to bin folder e.g. ~/Data/Logs
                    ? Path.Combine(details.InstallPath, folderpath.Remove(0, 1))
                    : folderpath;
            }
        }
    }
}
