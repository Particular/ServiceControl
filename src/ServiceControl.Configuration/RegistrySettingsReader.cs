namespace ServiceControl.Configuration;

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using static ValueConverter;

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
                        value = Convert<T>(keyValue);
                        return true;
                    }
                }

                rootKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

                using (var registryKey = rootKey.OpenSubKey(regPath))
                {
                    var keyValue = registryKey?.GetValue(name);

                    if (keyValue != null)
                    {
                        value = Convert<T>(keyValue);
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
                    value = Convert<T>(keyValue);
                    return true;
                }
            }
        }
        catch (Exception)
        {
            // intentionally swallow the exception so that we can fallback to the default value
        }

        value = default;
        return false;
    }
}