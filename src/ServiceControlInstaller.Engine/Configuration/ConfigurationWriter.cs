namespace ServiceControlInstaller.Engine.Configuration
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using ServiceControlInstaller.Engine.Instances;

    internal class ConfigurationWriter
    {
        IServiceControlInstance details;
        Configuration configuration;

        public ConfigurationWriter(IServiceControlInstance details)
        {
            this.details = details;
            var exeMapping = new ExeConfigurationFileMap { ExeConfigFilename = Path.Combine(details.InstallPath, "ServiceControl.exe.config") };
            configuration = ConfigurationManager.OpenMappedExeConfiguration(exeMapping, ConfigurationUserLevel.None);
        }

        public void Validate()
        {
            if (Transports.FindByName(details.TransportPackage) == null)
            {
                throw new Exception($"Invalid Transport - Must be one of: {string.Join(",", Transports.All.Select(p => p.Name))}");
            }
        }

        public void EnableMaintenanceMode()
        {
            var settings = configuration.AppSettings.Settings;
            settings.Set(SettingsList.MaintenanceMode, Boolean.TrueString, details.Version);
            configuration.Save();
        }

        public void DisableMaintenanceMode()
        {
            var settings = configuration.AppSettings.Settings;
            settings.Remove(SettingsList.MaintenanceMode.Name);
            configuration.Save();
        }

        public void Save()
        {
            configuration.ConnectionStrings.ConnectionStrings.Set("NServiceBus/Transport", details.ConnectionString);
            var settings = configuration.AppSettings.Settings;
            var version = details.Version;
            settings.Set(SettingsList.VirtualDirectory, details.VirtualDirectory);
            settings.Set(SettingsList.Port, details.Port.ToString());
            settings.Set(SettingsList.HostName, details.HostName);
            settings.Set(SettingsList.LogPath, details.LogPath);
            settings.Set(SettingsList.DBPath, details.DBPath);
            settings.Set(SettingsList.ForwardAuditMessages, details.ForwardAuditMessages.ToString());
            settings.Set(SettingsList.ForwardErrorMessages, details.ForwardErrorMessages.ToString(), version);
            settings.Set(SettingsList.TransportType, Transports.FindByName(details.TransportPackage).TypeName, version);
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
            
            configuration.Save();
        }

        void UpdateRuntimeSection()
        {
            
            var runtimesection = configuration.GetSection("runtime");
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
    }
}
