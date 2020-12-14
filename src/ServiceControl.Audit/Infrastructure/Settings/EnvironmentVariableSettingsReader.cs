namespace ServiceControl.Audit.Infrastructure.Settings
{
    using System;

    static class EnvironmentVariableSettingsReader<T>
    {
        public static T Read(string name, T defaultValue = default)
        {
            return Read("ServiceControl", name, defaultValue);
        }

        public static T Read(string root, string name, T defaultValue = default)
        {
            if (TryRead(root, name, out var value))
            {
                return value;
            }

            return defaultValue;
        }

        public static bool TryRead(string root, string name, out T value)
        {
            if (TryReadVariable(out value, $"{root}/{name}")) return true;
            if (TryReadVariable(out value, $"{root}_{name}")) return true;

            value = default;
            return false;
        }

        private static bool TryReadVariable(out T value, string fullKey)
        {
            var environmentValue = Environment.GetEnvironmentVariable(fullKey);

            if (environmentValue != null)
            {
                value = (T)Convert.ChangeType(environmentValue, typeof(T));
                return true;
            }

            value = default;
            return false;
        }

    }
}