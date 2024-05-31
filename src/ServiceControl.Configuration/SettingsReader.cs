namespace ServiceControl.Configuration;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

public static class SettingsReader
{
    public static T Read<T>(SettingsRootNamespace settingsNamespace, string name, T defaultValue = default)
        => TryRead<T>(settingsNamespace, name, out var value) ? value : defaultValue;

    public static bool TryRead<T>(SettingsRootNamespace settingsNamespace, string name, [NotNullWhen(true)] out T value)
    {
        if (EnvironmentVariableSettingsReader.TryRead<T>(settingsNamespace, name, out var envValue))
        {
            value = envValue;
            return true;
        }

        if (!AppEnvironment.RunningInContainer)
        {
            if (ConfigFileSettingsReader.TryRead<T>(settingsNamespace, name, out var configValue))
            {
                value = configValue;
                return true;
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RegistrySettingsReader.TryRead<T>(settingsNamespace, name, out var regValue))
        {
            value = regValue;
            return true;
        }

        value = default;
        return false;
    }
}