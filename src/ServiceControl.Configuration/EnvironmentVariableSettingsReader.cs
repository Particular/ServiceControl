namespace ServiceControl.Configuration;

using System;

static class EnvironmentVariableSettingsReader
{
    public static T Read<T>(string root, string name, T defaultValue = default) =>
        TryRead<T>(root, name, out var value)
            ? value
            : defaultValue;

    public static bool TryRead<T>(string root, string name, out T value)
    {
        if (TryReadVariable(out value, $"{root}/{name}"))
        {
            return true;
        }
        // Azure container instance compatibility:
        if (TryReadVariable(out value, $"{root}_{name}".Replace('.', '_')))
        {
            return true;
        }
        // container images and env files compatibility:
        if (TryReadVariable(out value, $"{root}_{name}".Replace('.', '_').Replace('/', '_')))
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
            value = (T)Convert.ChangeType(environmentValue, typeof(T));
            return true;
        }

        value = default;
        return false;
    }
}