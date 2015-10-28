namespace ServiceControlInstaller.Engine.Configuration
{
    using System;
    using System.Configuration;

    static class ConfigurationSectionExtensions
    {
        public static void Set(this ConnectionStringSettingsCollection collection, string key, string value)
        {
            collection.Remove(key);
            if (!String.IsNullOrWhiteSpace(value))
            {
                collection.Add(new ConnectionStringSettings(key, value));
            }
        }
        
        public static void Set(this KeyValueConfigurationCollection collection, string key, string value)
        {
            collection.Remove(key);
            if (!String.IsNullOrWhiteSpace(value))
            {
                collection.Add(new KeyValueConfigurationElement(key, value));
            }
        }
    }
}