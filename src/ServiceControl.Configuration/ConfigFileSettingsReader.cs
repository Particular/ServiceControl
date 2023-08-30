namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.Configuration;

    class ConfigFileSettingsReader : ISettingsReader
    {
        public object Read(string name, Type type, object defaultValue = default)
        {
            return Read("ServiceControl", name, type, defaultValue);
        }

        public object Read(string root, string name, Type type, object defaultValue = default)
        {
            if (TryRead(root, name, type, out var value))
            {
                return value;
            }

            return defaultValue;
        }

        public bool TryRead(string root, string name, Type type, out object value)
        {
            var fullKey = $"{root}/{name}";

            var appSettingValue = ConfigurationManager.AppSettings[fullKey];
            if (appSettingValue != null)
            {
                appSettingValue = Environment.ExpandEnvironmentVariables(appSettingValue); // TODO: Just added this to have expansing on appsettings to not have hardcoded "temp" paths which are different for everyone.
                value = Convert.ChangeType(appSettingValue, type);
                return true;
            }

            value = default;
            return false;
        }
    }
}