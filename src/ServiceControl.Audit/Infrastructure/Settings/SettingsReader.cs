namespace ServiceControl.Audit.Infrastructure.Settings
{
    class SettingsReader<T>
    {
        public static T Read(string name, T defaultValue = default)
        {
            return Read("ServiceControl.Audit", name, defaultValue);
        }

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

            return RegistryReader<T>.Read(root, name, defaultValue);
        }
    }
}