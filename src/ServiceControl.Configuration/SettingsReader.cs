namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;

    static class SettingsReader
    {
        static readonly ISettingsReader EnvironmentVariable = new EnvironmentVariableSettingsReader();
        static readonly ISettingsReader Registry = new RegistryReader();
        public static readonly ISettingsReader ConfigFile = new ConfigFileSettingsReader();

        public static T Read<T>(string name, T defaultValue = default)
        {
            return Read("ServiceControl", name, defaultValue);
        }

        public static T Read<T>(string root, string name, T defaultValue = default)
        {
            return (T)Read(root, name, typeof(T), defaultValue);
        }

        static object Read(string root, string name, Type type, object defaultValue = default)
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

        public static T Read<T>(this ISettingsReader instance, string name, T defaultValue = default)
        {
            return (T)instance.Read("ServiceControl", name, typeof(T), defaultValue);
        }
    }
}