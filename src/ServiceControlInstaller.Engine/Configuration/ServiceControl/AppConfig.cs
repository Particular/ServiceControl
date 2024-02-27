namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System.Configuration;
    using System.Linq;
    using NuGet.Versioning;

    public abstract class AppConfig : AppConfigWrapper
    {
        protected AppConfig(string configFilePath) : base(configFilePath)
        {
        }

        public void Save()
        {
            UpdateSettings();

            Config.Save();
        }

        public abstract void EnableMaintenanceMode();

        public abstract void DisableMaintenanceMode();

        public abstract void SetTransportType(string transportTypeName);

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