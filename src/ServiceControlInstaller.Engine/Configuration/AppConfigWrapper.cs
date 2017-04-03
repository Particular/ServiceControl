namespace ServiceControlInstaller.Engine.Configuration
{
    using System;
    using System.Configuration;
    using System.Linq;

    public class AppConfigWrapper
    {
        public Configuration Config;

        public AppConfigWrapper(string configFilePath)
        {
            var mapping = new ExeConfigurationFileMap { ExeConfigFilename = configFilePath };
            Config = ConfigurationManager.OpenMappedExeConfiguration(mapping, ConfigurationUserLevel.None);
        }

        public T Read<T>(SettingInfo keyInfo, T defaultValue)
        {
            return Read(keyInfo.Name, defaultValue);
        }

        public T Read<T>(string key, T defaultValue)
        {
            if (Config.AppSettings.Settings.AllKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                return (T)Convert.ChangeType(Config.AppSettings.Settings[key].Value, typeof(T));
            }

            try
            {
                var parts = key.Split("/".ToCharArray(), 2);
                return RegistryReader<T>.Read(parts[0], parts[1], defaultValue);
            }
            catch (Exception)
            {
                // Fall through to default
            }

            return defaultValue;
        }
    }
}

