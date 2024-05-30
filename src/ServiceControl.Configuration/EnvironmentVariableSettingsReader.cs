namespace ServiceControl.Configuration;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
        foreach (var fullKey in GetFullKeyOptions(settingsNamespace, name, RuntimeInformation.IsOSPlatform(OSPlatform.Windows)))
        {
            if (TryReadVariable(fullKey, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    static IEnumerable<string> GetFullKeyOptions(SettingsRootNamespace settingsNamespace, string name, bool isWindows)
    {
        if (isWindows)
        {
            yield return $"{settingsNamespace}/{name}";
        }
        else
        {
            var regex = SeparatorsRegex();

            var namespacedMixedCase = regex.Replace($"{settingsNamespace}_{name}", "_");
            yield return namespacedMixedCase.ToUpperInvariant();
            yield return namespacedMixedCase;

            var mixedCase = regex.Replace(name, "_");
            yield return mixedCase.ToUpperInvariant();
            yield return mixedCase;
        }
    }

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