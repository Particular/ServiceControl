namespace ServiceControl.Configuration;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static ValueConverter;

static partial class EnvironmentVariableSettingsReader
{
    public static T Read<T>(SettingsRootNamespace settingsNamespace, string name, T defaultValue = default) =>
        TryRead<T>(settingsNamespace, name, out var value)
            ? value
            : defaultValue;

    public static bool TryRead<T>(SettingsRootNamespace settingsNamespace, string name, out T value)
    {
        foreach (var fullKey in GetFullKeyOptions(settingsNamespace, name))
        {
            if (TryReadVariable(fullKey, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    static IEnumerable<string> GetFullKeyOptions(SettingsRootNamespace settingsNamespace, string name)
    {
        var regex = SeparatorsRegex();

        var namespacedKey = regex.Replace($"{settingsNamespace}_{name}", "_");
        yield return namespacedKey.ToUpperInvariant();
        yield return namespacedKey;

        // The 3 app namespaces can be loaded from env vars without the namespace, so settings like TransportType can be
        // shared between all instances in a container .env file. Settings in all other namespaces must be fully specified.
        if (CanSetWithoutNamespaceList.Contains(settingsNamespace))
        {
            var nameOnly = regex.Replace(name, "_");
            yield return nameOnly.ToUpperInvariant();
            yield return nameOnly;
        }
    }

    static readonly HashSet<SettingsRootNamespace> CanSetWithoutNamespaceList =
    [
        new SettingsRootNamespace("ServiceControl"),
        new SettingsRootNamespace("ServiceControl.Audit"),
        new SettingsRootNamespace("Monitoring")
    ];

    [GeneratedRegex(@"[\./]")]
    private static partial Regex SeparatorsRegex();

    static bool TryReadVariable<T>(string fullKey, out T value)
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