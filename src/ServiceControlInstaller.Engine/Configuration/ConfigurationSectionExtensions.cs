namespace ServiceControlInstaller.Engine.Configuration
{
    using System;
    using System.Configuration;
    using System.Linq;

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
        
        public static void Set(this KeyValueConfigurationCollection collection, string key, string value)
        {
            collection.Remove(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                collection.Add(new KeyValueConfigurationElement(key, value));
            }
        }
        /// <summary>
        /// if the key matches a value in unsupportedKeys then this is a no-op.  This is provide some backward compat when a new SCMU is editing an old SC instance
        /// </summary>
        public static void Set(this KeyValueConfigurationCollection collection, string key, string value, string[] unsupportedKeys)
        {
            if ((unsupportedKeys != null) && (unsupportedKeys.Any(p => p.Equals(key, StringComparison.OrdinalIgnoreCase))))
                    return;

            collection.Remove(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                collection.Add(new KeyValueConfigurationElement(key, value));
            }
        }
    }
}