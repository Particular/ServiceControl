namespace ServiceControl.Configuration;

using System;
using System.Configuration;
using static ValueConverter;

static class ConfigFileSettingsReader
{
    public static T Read<T>(SettingsRootNamespace settingsNamespace, string name, T defaultValue = default) =>
        TryRead<T>(settingsNamespace, name, out var value)
            ? value
            : defaultValue;

    public static bool TryRead<T>(SettingsRootNamespace settingsNamespace, string name, out T value)
    {
        var fullKey = $"{settingsNamespace}/{name}";

        var appSettingValue = ConfigurationManager.AppSettings[fullKey];
        if (appSettingValue != null)
        {
            appSettingValue = Environment.ExpandEnvironmentVariables(appSettingValue);
            value = Convert<T>(appSettingValue);
            return true;
        }

        value = default;
        return false;
    }
}