namespace ServiceBus.Management.Infrastructure.Settings
{
    public class SettingsReader<T>
    {
        public static T Read(string name, T defaultValue = default)
        {
            return Read("ServiceControl", name, defaultValue);
        }

        public static T Read(string root, string name, T defaultValue = default)
        {
            if (ConfigFileSettingsReader<T>.TryRead(root, name, out var value))
            {
                return value;
            }

            return RegistryReader<T>.Read(root, name, defaultValue);
        }
    }
}