namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.Configuration;

    public static class ConfigFileSettingsReader<T>
    {
        public static T Read(string name, T defaultValue = default(T))
        {
            return Read("ServiceControl", name, defaultValue);
        }

        public static T Read(string root, string name, T defaultValue = default(T))
        {
            T value;
            if (TryRead(root, name, out value))
            {
                return value;
            }
            return defaultValue;
        }

        public static bool TryRead(string root, string name, out T value)
        {
            var fullKey = $"{root}/{name}";

            if (ConfigurationManager.AppSettings[fullKey] != null)
            {
                value = (T)Convert.ChangeType(ConfigurationManager.AppSettings[fullKey], typeof(T));
                return true;
            }

            value = default(T);
            return false;
        }
    }
}