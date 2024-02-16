namespace ServiceControl.Configuration;

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using NServiceBus.Logging;

static class RegistrySettingsReader
{
    public static T Read<T>(SettingsRootNamespace settingsNamespace, string name, T defaultValue = default) =>
        TryRead<T>(settingsNamespace, name, out var value)
            ? value
            : defaultValue;

    public static bool TryRead<T>(SettingsRootNamespace settingsNamespace, string name, out T value)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Reading the registry is only supported on Windows");
        }

        var regPath = @"SOFTWARE\ParticularSoftware\" + settingsNamespace.ToString().Replace("/", "\\");
        try
        {
            if (Environment.Is64BitOperatingSystem)
            {
                var rootKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

                using (var registryKey = rootKey.OpenSubKey(regPath))
                {
                    var keyValue = registryKey?.GetValue(name);

                    if (keyValue != null)
                    {
                        value = (T)Convert.ChangeType(keyValue, typeof(T));
                        return true;
                    }
                }

                rootKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

                using (var registryKey = rootKey.OpenSubKey(regPath))
                {
                    var keyValue = registryKey?.GetValue(name);

                    if (keyValue != null)
                    {
                        value = (T)Convert.ChangeType(keyValue, typeof(T));
                        return true;
                    }
                }
            }
            else
            {
                var rootKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default);

                using var registryKey = rootKey.OpenSubKey(regPath);
                var keyValue = registryKey?.GetValue(name);

                if (keyValue != null)
                {
                    value = (T)Convert.ChangeType(keyValue, typeof(T));
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            // TODO check if we really want to keep NServiceBus logging around or have another way to deal with this
            Logger.Warn($"Couldn't read the registry to retrieve the {name}, from '{regPath}'.", ex);
        }

        value = default;
        return false;
    }

    static readonly ILog Logger = LogManager.GetLogger(typeof(RegistrySettingsReader));
}