namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;

    static class SettingsReaderExtensions
    {
        public static T Read<T>(this ISettingsReader instance, string name, T defaultValue = default)
        {
            return (T)instance.Read("ServiceControl", name, typeof(T), defaultValue);
        }

        public static T Read<T>(this ISettingsReader instance, string root, string name, T defaultValue = default)
        {
            if (instance.TryRead(root, name, typeof(T), out var value))
            {
                return (T)value;
            }

            return defaultValue;
        }

        public static bool TryRead<T>(this ISettingsReader instance, string root, string name, out T value)
        {
            if (instance.TryRead(root, name, typeof(T), out object innerValue))
            {
                value = (T)innerValue;
                return true;
            }

            value = default;
            return false;
        }

        public static object Read(this ISettingsReader instance, string name, Type type, object defaultValue = null)
        {
            return instance.Read("ServiceControl", name, type, defaultValue);
        }
    }
}