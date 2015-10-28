namespace ServiceControlInstaller.Engine.Configuration
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using ServiceControlInstaller.Engine.Instances;

    internal class ConfigurationWriter
    {
        ServiceControlInstanceMetadata details;
        Configuration configuration;

        public ConfigurationWriter(ServiceControlInstanceMetadata details)
        {
            this.details = details;
            var exeMapping = new ExeConfigurationFileMap { ExeConfigFilename = Path.Combine(details.InstallPath, "ServiceControl.exe.config") };
            configuration = ConfigurationManager.OpenMappedExeConfiguration(exeMapping, ConfigurationUserLevel.None);
        }

        public void Validate()
        {
            if (Transports.FindByName(details.TransportPackage) == null)
            {
                throw new Exception(string.Format("Invalid Transport - Must be one of: {0}", string.Join(",", Transports.All.Select(p => p.Name))));
            }
        }
        
        public void Save()
        {
            configuration.ConnectionStrings.ConnectionStrings.Set("NServiceBus/Transport", details.ConnectionString);
            var settings = configuration.AppSettings.Settings;
            settings.Set("ServiceControl/VirtualDirectory", details.VirtualDirectory);
            settings.Set("ServiceControl/Port", details.Port.ToString());
            settings.Set("ServiceControl/HostName", details.HostName);
            settings.Set("ServiceControl/LogPath", details.LogPath);
            settings.Set("ServiceControl/DBPath", details.DBPath);
            settings.Set("ServiceControl/ForwardAuditMessages", details.ForwardAuditMessages.ToString());
            settings.Set("ServiceControl/TransportType", Transports.FindByName(details.TransportPackage).TypeName);
            settings.Set("ServiceBus/AuditQueue", details.AuditQueue);
            settings.Set("ServiceBus/ErrorQueue", details.ErrorQueue);
            settings.Set("ServiceBus/ErrorLogQueue", details.ErrorLogQueue);
            settings.Remove("ServiceBus/AuditLogQueue");
            settings.Set("ServiceBus/AuditLogQueue", details.AuditLogQueue);
            configuration.Save();
        }
    }
}
