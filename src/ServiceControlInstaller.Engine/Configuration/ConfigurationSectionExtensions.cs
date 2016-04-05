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
            if (keyInfo.SupportedFrom != null || keyInfo.RemovedFrom != null)
            {
                if (currentVersion == null)
                    throw new ArgumentNullException("currentVersion", string.Format("Version info is required before setting {0} as it's not applicable to all versions", keyInfo.Name));
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
    }
}