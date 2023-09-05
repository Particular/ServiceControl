namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.ComponentModel;

    static class SettingsReader
    {
        const string Namespace = "ServiceControl";
        static readonly ISettingsReader EnvironmentVariable = new EnvironmentVariableSettingsReader();
        static readonly ISettingsReader Registry = new RegistryReader();
        public static readonly ISettingsReader ConfigFile = new ConfigFileSettingsReader();

        public static T Read<T>(string name, T defaultValue = default)
        {
            return Read(Namespace, name, defaultValue);
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

        public static bool TryRead(string name, Type type, out object value)
        {
            var root = Namespace;

            if (EnvironmentVariable.TryRead(root, name, type, out var envValue))
            {
                value = envValue;
                return true;
            }

            if (ConfigFile.TryRead(root, name, type, out var configValue))
            {
                value = configValue;
                return true;
            }

            if (Registry.TryRead(root, name, type, out var regValue))
            {
                value = regValue;
                return true;
            }

            value = null;
            return false;
        }

        public static T Read<T>(this ISettingsReader instance, string name, T defaultValue = default)
        {
            return (T)instance.Read(Namespace, name, typeof(T), defaultValue);
        }

        public static object ConvertFrom(object sourceValue, Type destinationType)
        {
            var converter = TypeDescriptor.GetConverter(destinationType);
            object value = converter.ConvertFrom(sourceValue);
            return value;
        }

    }
}