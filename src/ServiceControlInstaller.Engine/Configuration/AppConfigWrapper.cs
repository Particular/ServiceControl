namespace ServiceControlInstaller.Engine.Configuration
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Linq;

    public class AppConfigWrapper
    {
        public AppConfigWrapper(string configFilePath)
        {
            ConfigFilePath = configFilePath;

            try
            {
                var mapping = new ExeConfigurationFileMap { ExeConfigFilename = configFilePath };
                Config = ConfigurationManager.OpenMappedExeConfiguration(mapping, ConfigurationUserLevel.None);
            }
            catch (ConfigurationErrorsException ex)
            {
                // Log the XML parsing error to the NServiceBus log file
                ConfigLoadException = ex;
                LogXmlConfigurationError(ex);
                // Don't re-throw - let the caller handle the error via ConfigLoadException
            }
        }

        void LogXmlConfigurationError(Exception ex)
        {
            try
            {
                // Write error to config-error.txt in the same directory as the config file
                // This follows the same pattern as NLog but with a dedicated file for config errors
                var configDirectory = Path.GetDirectoryName(ConfigFilePath);
                var logFilePath = Path.Combine(configDirectory, "config-error.txt");

                ConfigErrorLogPath = logFilePath;

                // Write the error to the log file
                var logMessage = $@"
================================================================================
[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] CONFIGURATION XML PARSING ERROR
================================================================================
Configuration file: {ConfigFilePath}
Error: {ex.Message}

Exception Details:
{ex}
================================================================================
";
                File.AppendAllText(logFilePath, logMessage);
            }
            catch
            {
                // If logging fails, don't throw - the main exception will still be propagated
            }
        }

        public T Read<T>(SettingInfo keyInfo, T defaultValue)
        {
            return Read(keyInfo.Name, defaultValue);
        }

        public T Read<T>(string key, T defaultValue)
        {
            if (Config?.AppSettings.Settings.AllKeys.Contains(key, StringComparer.OrdinalIgnoreCase) == true)
            {
                var nonNullableType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                return (T)Convert.ChangeType(Config.AppSettings.Settings[key].Value, nonNullableType);
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

        public bool AppSettingExists(string key)
        {
            return Config?.AppSettings.Settings.AllKeys.Contains(key, StringComparer.OrdinalIgnoreCase) == true;
        }

        public Configuration Config;
        public string ConfigFilePath;
        public string ConfigErrorLogPath;
        public Exception ConfigLoadException;
    }
}