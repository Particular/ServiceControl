namespace ServiceControlInstaller.Engine.Configuration
{
    using System;
    using System.Configuration;
    using NuGet.Versioning;

    static class ConfigurationSectionExtensions
    {
        public static void Set(this ConnectionStringSettingsCollection collection, string key, string value)
        {
            collection.Remove(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                collection.Add(new ConnectionStringSettings(key, value));
            }
        }

        public static void Set(this KeyValueConfigurationCollection collection, SettingInfo keyInfo, string value, SemanticVersion currentVersion = null)
        {
            // If either SupportedFrom or RemovedFrom exists then we need the currentVersion
            if (keyInfo.SupportedFrom != null || keyInfo.RemovedFrom != null)
            {
                if (currentVersion == null)
                {
                    throw new ArgumentNullException(nameof(currentVersion), $"Version info is required before setting or removing {keyInfo.Name}");
                }
            }

            collection.Remove(keyInfo.Name);

            // Using VersionComparer.Version to compare versions and ignore release info (i.e. -alpha.1)
            if (keyInfo.SupportedFrom != null && VersionComparer.Version.Compare(currentVersion, keyInfo.SupportedFrom) < 0)
            {
                return;
            }

            // Using VersionComparer.Version to compare versions and ignore release info (i.e. -alpha.1)
            if (keyInfo.RemovedFrom != null && VersionComparer.Version.Compare(currentVersion, keyInfo.RemovedFrom) >= 0)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                collection.Add(new KeyValueConfigurationElement(keyInfo.Name, value));
            }
        }


        public static void RemoveIfRetired(this KeyValueConfigurationCollection collection, SettingInfo keyInfo, SemanticVersion currentVersion)
        {
            if (keyInfo.RemovedFrom == null)
            {
                return;
            }

            if (currentVersion == null)
            {
                throw new ArgumentNullException(nameof(currentVersion), $"Version info is required before setting or removing {keyInfo.Name}");
            }

            // Using VersionComparer.Version to compare versions and ignore release info (i.e. -alpha.1)
            var isObsolete = VersionComparer.Version.Compare(currentVersion, keyInfo.RemovedFrom) >= 0;

            if (isObsolete)
            {
                collection.Remove(keyInfo.Name);
            }
        }
    }
}