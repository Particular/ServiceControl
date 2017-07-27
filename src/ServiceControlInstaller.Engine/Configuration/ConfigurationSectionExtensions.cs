namespace ServiceControlInstaller.Engine.Configuration
{
    using System;
    using System.Configuration;

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

        public static void Set(this KeyValueConfigurationCollection collection, SettingInfo keyInfo, string value, Version currentVersion = null)
        {
            // If either SupportedFrom or RemovedFrom exists then we need the currentVersion
            if (keyInfo.SupportedFrom != null || keyInfo.RemovedFrom != null)
            {
                if (currentVersion == null)
                    throw new ArgumentNullException(nameof(currentVersion), $"Version info is required before setting or removing {keyInfo.Name}");
            }

            collection.Remove(keyInfo.Name);

            if (keyInfo.SupportedFrom != null && currentVersion < keyInfo.SupportedFrom)
                return;

            if (keyInfo.RemovedFrom != null && currentVersion >= keyInfo.RemovedFrom)
                return;

            if (!string.IsNullOrWhiteSpace(value))
            {
                collection.Add(new KeyValueConfigurationElement(keyInfo.Name, value));
            }
        }


        public static void RemoveIfRetired(this KeyValueConfigurationCollection collection, SettingInfo keyInfo, Version currentVersion)
        {
            if (keyInfo.RemovedFrom == null)
                return;

            if (currentVersion == null)
            {
               throw new ArgumentNullException(nameof(currentVersion), $"Version info is required before setting or removing {keyInfo.Name}");
            }

            if (currentVersion >= keyInfo.RemovedFrom)
            {
                collection.Remove(keyInfo.Name);
            }
        }
    }
}