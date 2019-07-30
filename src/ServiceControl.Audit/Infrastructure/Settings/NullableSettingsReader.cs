namespace ServiceControl.Audit.Infrastructure.Settings
{
    using System;
    using System.Configuration;

    class NullableSettingsReader<T> where T : struct
    {
        public static T? Read(string name)
        {
            return Read("ServiceControl.Audit", name, null);
        }

        public static T? Read(string root, string name, T? defaultValue)
        {
            var fullKey = root + "/" + name;

            if (ConfigurationManager.AppSettings[fullKey] != null)
            {
                return (T)Convert.ChangeType(ConfigurationManager.AppSettings[fullKey], typeof(T));
            }

            return RegistryReader<T?>.Read(root, name, defaultValue);
        }
    }
}