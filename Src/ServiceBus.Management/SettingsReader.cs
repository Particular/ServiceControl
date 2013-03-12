namespace ServiceBus.Management
{
    using System;
    using System.Configuration;
    using NServiceBus.Utils;

    public class SettingsReader<T>
    {
        public static T Read(string name, T defaultValue = default(T))
        {
            var fullKey = "ServiceBus/Management/" + name;

            if (ConfigurationManager.AppSettings[fullKey] != null)
                return (T)Convert.ChangeType(ConfigurationManager.AppSettings[fullKey], typeof(T));
           

            //todo: Pass in "Particular" as the root key when the core has been updated to allow for it
            return RegistryReader<T>.Read(name,defaultValue);
        }
    }
}