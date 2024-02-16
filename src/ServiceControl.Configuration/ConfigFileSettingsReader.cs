namespace ServiceControl.Configuration;

using System;
using System.Configuration;

static class ConfigFileSettingsReader
{
    public static T Read<T>(string root, string name, T defaultValue = default) =>
        TryRead<T>(root, name, out var value)
            ? value
            : defaultValue;

    public static bool TryRead<T>(string root, string name, out T value)
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