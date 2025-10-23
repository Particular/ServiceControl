namespace ServiceControl.Configuration;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using ConfigurationManager = System.Configuration.ConfigurationManager;

public sealed class SettingsReaderConfigurationProvider : ConfigurationProvider
{
    public override void Load()
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            LoadFromRegistry(data);
        }

        if (!AppEnvironment.RunningInContainer)
        {
            LoadFromConfigFile(data);
        }

        LoadFromEnvironmentVariables(data);

        Data = data;
    }

    void LoadFromEnvironmentVariables(Dictionary<string, string> data)
    {
        foreach (var settingsNamespace in KnownNamespaces)
        {
            // Get all environment variables
            var allEnvVars = Environment.GetEnvironmentVariables();

            foreach (System.Collections.DictionaryEntry entry in allEnvVars)
            {
                var envKey = entry.Key.ToString();
                if (string.IsNullOrEmpty(envKey))
                {
                    continue;
                }

                var envValue = entry.Value?.ToString();
                if (envValue == null)
                {
                    continue;
                }

                // Try to match this env var to a namespaced key
                var normalizedEnvKey = envKey.Replace("__", ":");

                // Check if it matches the pattern: NAMESPACE_NAME or just NAME (for allowed namespaces)
                var namespacedPattern = $"{settingsNamespace.Root.ToUpperInvariant()}_";
                var configKey = string.Empty;

                if (normalizedEnvKey.StartsWith(namespacedPattern, StringComparison.OrdinalIgnoreCase))
                {
                    // Extract the setting name after namespace
                    var settingName = normalizedEnvKey.Substring(namespacedPattern.Length);
                    configKey = $"{settingsNamespace.Root}:{settingName.Replace('_', ':')}";
                }
                else if (CanSetWithoutNamespaceList.Contains(settingsNamespace))
                {
                    // For allowed namespaces, also check without namespace prefix
                    configKey = $"{settingsNamespace.Root}:{normalizedEnvKey.Replace('_', ':')}";
                }

                if (!string.IsNullOrEmpty(configKey))
                {
                    var expandedValue = Environment.ExpandEnvironmentVariables(envValue);
                    data[configKey] = expandedValue;
                }
            }
        }
    }

    void LoadFromConfigFile(Dictionary<string, string> data)
    {
        foreach (var key in ConfigurationManager.AppSettings.AllKeys)
        {
            if (key == null)
            {
                continue;
            }

            // ConfigFile uses "/" separator, convert to ":" for configuration system
            var normalizedKey = key.Replace('/', ':');
            var value = ConfigurationManager.AppSettings[key];

            if (value != null)
            {
                var expandedValue = Environment.ExpandEnvironmentVariables(value);
                data[normalizedKey] = expandedValue;
            }
        }
    }

#pragma warning disable CA1416
    void LoadFromRegistry(Dictionary<string, string> data)
    {
        foreach (var settingsNamespace in KnownNamespaces)
        {
            var regPath = @"SOFTWARE\ParticularSoftware\" + settingsNamespace.Root.Replace("/", "\\");

            try
            {
                if (Environment.Is64BitOperatingSystem)
                {
                    LoadFromRegistryView(data, regPath, settingsNamespace, RegistryView.Registry64);
                    LoadFromRegistryView(data, regPath, settingsNamespace, RegistryView.Registry32);
                }
                else
                {
                    LoadFromRegistryView(data, regPath, settingsNamespace, RegistryView.Default);
                }
            }
            catch (Exception)
            {
                // Intentionally swallow exceptions to allow fallback behavior
            }
        }
    }

    void LoadFromRegistryView(Dictionary<string, string> data, string regPath, SettingsRootNamespace settingsNamespace, RegistryView view)
    {
        try
        {
            var rootKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var registryKey = rootKey.OpenSubKey(regPath);

            if (registryKey == null)
            {
                return;
            }

            foreach (var valueName in registryKey.GetValueNames())
            {
                var value = registryKey.GetValue(valueName);
                if (value != null)
                {
                    var configKey = $"{settingsNamespace.Root}:{valueName}";
                    // Only add if not already present (to respect priority from higher-priority views)
                    if (!data.ContainsKey(configKey))
                    {
                        data[configKey] = value.ToString();
                    }
                }
            }
        }
        catch (Exception)
        {
            // Intentionally swallow exceptions
        }
    }
#pragma warning restore CA1416

    static readonly HashSet<SettingsRootNamespace> CanSetWithoutNamespaceList =
    [
        new("ServiceControl"),
        new("ServiceControl.Audit"),
        new("Monitoring")
    ];

    static readonly SettingsRootNamespace[] KnownNamespaces =
    [
        new("ServiceControl"),
        new("ServiceControl.Audit"),
        new("Monitoring")
    ];
}