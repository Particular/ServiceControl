namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.Configuration;
    using Microsoft.Win32;
    using NServiceBus.Logging;

    public class SettingsReader<T>
    {
        public static T Read(string name, T defaultValue = default(T))
        {
            return Read("ServiceControl", name, defaultValue);
        }

        public static T Read(string root, string name, T defaultValue = default(T))
        {
            var fullKey = root + "/" + name;

            if (ConfigurationManager.AppSettings[fullKey] != null)
            {
                return (T) Convert.ChangeType(ConfigurationManager.AppSettings[fullKey], typeof(T));
            }

            //todo: Pass in "Particular" as the root key when the core has been updated to allow for it
            return RegistryReader<T>.Read(root, name, defaultValue);
        }
    }

    /// <summary>
    ///     Wrapper to read registry keys.
    /// </summary>
    /// <typeparam name="T">The type of the key to retrieve</typeparam>
    public class RegistryReader<T>
    {
        /// <summary>
        ///     Attempts to read the key from the registry.
        /// </summary>
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
                        if (registryKey != null)
                        {
                            var value = registryKey.GetValue(name);

                            if (value != null)
                            {
                                return (T)Convert.ChangeType(value, typeof(T));
                            }
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
                Logger.Warn(
                    string.Format(@"We couldn't read the registry to retrieve the {0}, from '{1}'.", name, regPath), ex);
            }

            return defaultValue;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(RegistryReader<T>));
    }
}