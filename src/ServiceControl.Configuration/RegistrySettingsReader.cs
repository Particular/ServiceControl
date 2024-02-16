namespace ServiceControl.Configuration;

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using NServiceBus.Logging;

static class RegistrySettingsReader
{
    public static T Read<T>(string subKey, string name, T defaultValue = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Reading the registry is only supported on Windows");
        }

        var regPath = @"SOFTWARE\ParticularSoftware\" + subKey.Replace("/", "\\");
        try
        {
            if (Environment.Is64BitOperatingSystem)
            {
                var rootKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

                using (var registryKey = rootKey.OpenSubKey(regPath))
                {
                    var value = registryKey?.GetValue(name);

                    if (value != null)
                    {
                        return (T)Convert.ChangeType(value, typeof(T));
                    }
                }

                rootKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

                using (var registryKey = rootKey.OpenSubKey(regPath))
                {
                    if (registryKey != null)
                    {
                        return (T)Convert.ChangeType(registryKey.GetValue(name, defaultValue), typeof(T));
                    }
                }
            }
            else
            {
                var rootKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default);

                using var registryKey = rootKey.OpenSubKey(regPath);
                if (registryKey != null)
                {
                    return (T)Convert.ChangeType(registryKey.GetValue(name, defaultValue), typeof(T));
                }
            }
        }
        catch (Exception ex)
        {
            // TODO check if we really want to keep NServiceBus logging around or have another way to deal with this
            Logger.Warn($"Couldn't read the registry to retrieve the {name}, from '{regPath}'.", ex);
        }

        return defaultValue;
    }

    public static bool TryRead<T>(string root, string name, out T value)
    {
        value = Read<T>(root, name);
        return value != null;
    }

    static readonly ILog Logger = LogManager.GetLogger(typeof(RegistrySettingsReader));
}