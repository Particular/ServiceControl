namespace ServiceControl.Configuration;

using System;
using static ValueConverter;

static class EnvironmentVariableSettingsReader
{
    public static T Read<T>(SettingsRootNamespace settingsNamespace, string name, T defaultValue = default) =>
        TryRead<T>(settingsNamespace, name, out var value)
            ? value
            : defaultValue;

    public static bool TryRead<T>(SettingsRootNamespace settingsNamespace, string name, out T value)
    {
        if (TryReadVariable(out value, $"{settingsNamespace}/{name}"))
        {
            return true;
        }
        // Azure container instance compatibility:
        if (TryReadVariable(out value, $"{settingsNamespace}_{name}".Replace('.', '_')))
        {
            return true;
        }
        // container images and env files compatibility:
        if (TryReadVariable(out value, $"{settingsNamespace}_{name}".Replace('.', '_').Replace('/', '_')))
        {
            return true;
        }

        // POSIX compliant https://stackoverflow.com/a/2821183
        if (TryReadVariable(out value,
                $"{settingsNamespace}_{name}".Replace('.', '_').Replace('/', '_').ToUpperInvariant()))
        {
            return true;
        }

        value = default;
        return false;
    }

    static bool TryReadVariable<T>(out T value, string fullKey)
    {
        var environmentValue = Environment.GetEnvironmentVariable(fullKey);

        if (environmentValue != null)
        {
            environmentValue = Environment.ExpandEnvironmentVariables(environmentValue);
            value = Convert<T>(environmentValue);
            return true;
        }

        value = default;
        return false;
    }
}