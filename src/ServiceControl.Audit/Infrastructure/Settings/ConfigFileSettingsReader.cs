namespace ServiceControl.Audit.Infrastructure.Settings
{
    using System;
    using System.Configuration;

    static class ConfigFileSettingsReader<T>
    {
        public static T Read(string name, T defaultValue = default)
        {
            return Read("ServiceControl.Audit", name, defaultValue);
        }

        public static T Read(string root, string name, T defaultValue = default)
        {
            if (TryRead(root, name, out var value))
            {
                return value;
            }

            return defaultValue;
        }

        public static bool TryRead(string root, string name, out T value)
        {
            var fullKey = $"{root}/{name}";

            var appSettingValue = ConfigurationManager.AppSettings[fullKey];
            if (appSettingValue != null)
            {
                appSettingValue = Environment.ExpandEnvironmentVariables(appSettingValue);
                value = (T)Convert.ChangeType(appSettingValue, typeof(T));
                return true;
            }

            value = default;
            return false;
        }
    }
}