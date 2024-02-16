namespace ServiceControl.Configuration;

using System;
using System.Configuration;

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
            value = (T)Convert.ChangeType(appSettingValue, typeof(T));
            return true;
        }

        value = default;
        return false;
    }
}