namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.IO;
    using NLog;
    using NLog.Common;

    public class LoggingSettings
    {
        public LoggingSettings(string serviceName)
        {
            LoggingLevel = InitializeLevel("LogLevel");
            RavenDBLogLevel = InitializeLevel("RavenDBLogLevel");
            LogPath = Environment.ExpandEnvironmentVariables(ConfigFileSettingsReader<string>.Read("LogPath", DefaultLogPathForInstance(serviceName)));
        }

        LogLevel InitializeLevel(string key)
        {
            var level = LogLevel.Warn;
            try
            {
                level = LogLevel.FromString(ConfigFileSettingsReader<string>.Read(key, LogLevel.Warn.Name));
            }
            catch
            {
                InternalLogger.Warn($"Failed to parse {key} setting. Defaulting to Warn.");
            }
            return level;
        }

        public LogLevel LoggingLevel { get; }

        public LogLevel RavenDBLogLevel { get; }

        public string LogPath { get; }

        private static string DefaultLogPathForInstance(string serviceName)
        {
            if (serviceName.Equals(Settings.DEFAULT_SERVICE_NAME, StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Particular\\ServiceControl\\logs");
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), $"Particular\\{serviceName}\\logs");
        }
    }
}