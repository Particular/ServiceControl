namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.Configuration;

    class ConfigFileSettingsReader : ISettingsReader
    {
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

                var underlyingType = Nullable.GetUnderlyingType(type);

                var destinationType = underlyingType ?? type;

                value = SettingsReader.ConvertFrom(appSettingValue, destinationType);

                return true;
            }

            value = default;
            return false;
        }
    }
}