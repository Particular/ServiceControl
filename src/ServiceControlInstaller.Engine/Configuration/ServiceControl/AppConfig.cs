namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System.Configuration;
    using System.Linq;
    using System.Xml.Linq;
    using NuGet.Versioning;

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


        public abstract void EnableMaintenanceMode();

        public abstract void DisableMaintenanceMode();

        public abstract void SetTransportType(string transportTypeName);

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

        protected static void RemoveRavenDB35Settings(KeyValueConfigurationCollection settings, SemanticVersion currentVersion)
        {
            var removeFrom = new SemanticVersion(5, 0, 0);

            // Using VersionComparer.Version to compare versions and ignore release info (i.e. -alpha.1)
            var isObsolete = VersionComparer.Version.Compare(currentVersion, removeFrom) >= 0;

            if (isObsolete)
            {
                foreach (var key in settings.AllKeys.Where(k => k.StartsWith("Raven/")))
                {
                    settings.Remove(key);
                }
            }
        }
    }
}