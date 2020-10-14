namespace ServiceControl.Monitoring.Infrastructure.Settings
{
    class SettingsReader<T>
    {
        public static T Read(string name, T defaultValue = default)
        {
            return Read("Monitoring", name, defaultValue);
        }

        public static T Read(string root, string name, T defaultValue = default)
        {
            if (EnvironmentVariableSettingsReader<T>.TryRead(root, name, out var envValue))
            {
                return envValue;
            }

            return ConfigFileSettingsReader<T>.Read(root, name, defaultValue);
        }
    }
}