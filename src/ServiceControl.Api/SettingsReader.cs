﻿namespace ServiceBus.Management.Infrastructure.Settings
{
    public class SettingsReader<T>
    {
        public static T Read(string name, T defaultValue = default(T))
        {
            return Read("ServiceControl", name, defaultValue);
        }

        public static T Read(string root, string name, T defaultValue = default(T))
        {
            T value;
            if (ConfigFileSettingsReader<T>.TryRead(root, name, out value))
            {
                return value;
            }

            return RegistryReader<T>.Read(root, name, defaultValue);
        }
    }
}