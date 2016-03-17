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
            settings.Set("ServiceControl/ForwardErrorMessages", details.ForwardErrorMessages.ToString());
            settings.Set("ServiceControl/TransportType", Transports.FindByName(details.TransportPackage).TypeName);
            settings.Set("ServiceBus/AuditQueue", details.AuditQueue);
            settings.Set("ServiceBus/ErrorQueue", details.ErrorQueue);
            settings.Set("ServiceBus/ErrorLogQueue", details.ErrorLogQueue);
            settings.Set("ServiceBus/AuditLogQueue", details.AuditLogQueue);

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
