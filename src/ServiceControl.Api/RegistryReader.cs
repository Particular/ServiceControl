namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using Microsoft.Win32;
    using NServiceBus.Logging;

    /// <summary>
    ///     Wrapper to read registry keys.
    /// </summary>
    /// <typeparam name="T">The type of the key to retrieve</typeparam>
    public class RegistryReader<T>
    {
        /// <summary>
        ///     Attempts to read the key from the registry.
        /// </summary>
        /// <param name="subKey">The subkey to target.</param>
        /// <param name="name">The name of the value to retrieve. This string is not case-sensitive.</param>
        /// <param name="defaultValue">The value to return if <paramref name="name" /> does not exist. </param>
        /// <returns>
        ///     The value associated with <paramref name="name" />, with any embedded environment variables left unexpanded, or
        ///     <paramref name="defaultValue" /> if <paramref name="name" /> is not found.
        /// </returns>
        public static T Read(string subKey, string name, T defaultValue = default(T))
        {
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

                    using (var registryKey = rootKey.OpenSubKey(regPath))
                    {
                        if (registryKey != null)
                        {
                            return (T)Convert.ChangeType(registryKey.GetValue(name, defaultValue), typeof(T));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($@"We couldn't read the registry to retrieve the {name}, from '{regPath}'.", ex);
            }

            return defaultValue;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(RegistryReader<T>));
    }
}