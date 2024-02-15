namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.Runtime.InteropServices;
    using Microsoft.Win32;
    using NServiceBus.Logging;

    /// <summary>
    /// Wrapper to read registry keys.
    /// </summary>
    class RegistryReader : ISettingsReader
    {
        /// <summary>
        /// Attempts to read the key from the registry.
        /// </summary>
        /// <param name="subKey">The subkey to target.</param>
        /// <param name="name">The name of the value to retrieve. This string is not case-sensitive.</param>
        /// <param name="defaultValue">The value to return if <paramref name="name" /> does not exist. </param>
        /// <returns>
        /// The value associated with <paramref name="name" />, with any embedded environment variables left unexpanded, or
        /// <paramref name="defaultValue" /> if <paramref name="name" /> is not found.
        /// </returns>
        public object Read(string subKey, string name, Type type, object defaultValue = default)
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
                            return SettingsReader.ConvertFrom(value, type);
                        }
                    }

                    rootKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

                    using (var registryKey = rootKey.OpenSubKey(regPath))
                    {
                        if (registryKey != null)
                        {
                            return SettingsReader.ConvertFrom(registryKey.GetValue(name, defaultValue), type);
                        }
                    }
                }
                else
                {
                    var rootKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default);

                    using (var registryKey = rootKey.OpenSubKey(regPath))
                    {
                        if (registryKey != null)
                        {
                            return SettingsReader.ConvertFrom(registryKey.GetValue(name, defaultValue), type);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Couldn't read the registry to retrieve the {name}, from '{regPath}'.", ex);
            }

            return defaultValue;
        }

        public bool TryRead(string root, string name, Type type, out object value)
        {
            value = Read(root, name, type);
            return value != null;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(RegistryReader));
    }
}