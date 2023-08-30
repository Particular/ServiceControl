namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;

    class SettingsReader
    {
        public static readonly EnvironmentVariableSettingsReader EnvironmentVariable = new EnvironmentVariableSettingsReader();
        public static readonly ConfigFileSettingsReader ConfigFile = new ConfigFileSettingsReader();
        public static readonly RegistryReader Registry = new RegistryReader();

        public static T Read<T>(string name, T defaultValue = default)
        {
            return Read("ServiceControl", name, defaultValue);
        }

        public static T Read<T>(string root, string name, T defaultValue = default)
        {
            return (T)Read(root, name, typeof(T), defaultValue);
        }

        //public static object Read(string name, Type type, object defaultValue = default)
        //{
        //    return Read("ServiceControl", name, type, defaultValue);
        //}

        public static object Read(string root, string name, Type type, object defaultValue = default)
        {
            if (EnvironmentVariable.TryRead(root, name, type, out var envValue))
            {
                return envValue;
            }

            if (ConfigFile.TryRead(root, name, type, out var value))
            {
                return value;
            }

            return Registry.Read(root, name, type, defaultValue);
        }

    }
}