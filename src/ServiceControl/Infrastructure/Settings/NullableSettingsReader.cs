namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.Configuration;
    
    public class NullableSettingsReader<T> where T : struct 
    {
        public static T? Read(string name)
        {
            return Read("ServiceControl", name, null);
        }

        public static T? Read(string root, string name, T? defaultValue)
        {
            var fullKey = root + "/" + name;

            if (ConfigurationManager.AppSettings[fullKey] != null)
            {
                return (T) Convert.ChangeType(ConfigurationManager.AppSettings[fullKey], typeof(T));
            }

            return RegistryReader<T?>.Read(root, name, defaultValue);
        }
    }
}