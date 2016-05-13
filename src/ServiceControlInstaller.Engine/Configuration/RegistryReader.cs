namespace ServiceControlInstaller.Engine.Configuration
{
    using System;
    using Microsoft.Win32;

    class RegistryReader<T>
    {
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
            catch 
            {
                // Give up and use default value
            }

            return defaultValue;
        }
        
    }
}