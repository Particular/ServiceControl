namespace ServiceControl.Audit.Infrastructure.Settings
{
    using System.Runtime.InteropServices;

    class SettingsReader<T>
    {
        public static T Read(string name, T defaultValue = default) => Read("ServiceControl.Audit", name, defaultValue);

        public static T Read(string root, string name, T defaultValue = default)
        {
            if (EnvironmentVariableSettingsReader<T>.TryRead(root, name, out var envValue))
            {
                return envValue;
            }

            if (ConfigFileSettingsReader<T>.TryRead(root, name, out var value))
            {
                return value;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return RegistryReader<T>.Read(root, name, defaultValue);
            }

            return defaultValue;
        }
    }
}