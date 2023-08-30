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
            return TryRead(root, name, type, out var value)
                ? value
                : defaultValue;
        }

        public bool TryRead(string root, string name, Type type, out object value)
        {
            var fullKey = $"{root}/{name}";

            var appSettingValue = ConfigurationManager.AppSettings[fullKey];
            if (appSettingValue != null)
            {
                appSettingValue = Environment.ExpandEnvironmentVariables(appSettingValue);
                value = Convert.ChangeType(appSettingValue, type);
                return true;
            }

            value = default;
            return false;
        }
    }
}