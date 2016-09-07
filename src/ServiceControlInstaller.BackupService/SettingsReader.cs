namespace ServiceControlInstaller.BackupStubService
{
    using System;
    using System.Configuration;

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
                return (T)Convert.ChangeType(ConfigurationManager.AppSettings[fullKey], typeof(T));
            }

            return defaultValue;
        }
    }
}
