namespace ServiceControl.Configuration;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

public static class SettingsReader
{
    public static T Read<T>(string root, string name, T defaultValue = default)
        => TryRead<T>(root, name, out var value) ? value : defaultValue;

    public static bool TryRead<T>(string root, string name, [NotNullWhen(true)] out T value)
    {
        if (EnvironmentVariableSettingsReader.TryRead<T>(root, name, out var envValue))
        {
            value = envValue;
            return true;
        }

        if (ConfigFileSettingsReader.TryRead<T>(root, name, out var configValue))
        {
            value = configValue;
            return true;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RegistrySettingsReader.TryRead<T>(root, name, out var regValue))
        {
            value = regValue;
            return true;
        }

        value = default;
        return false;
    }
}