namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.IO;
    using NLog;
    using NLog.Common;

    class LoggingSettings
    {
        public LoggingSettings(string serviceName, LogLevel defaultLevel = null, LogLevel defaultRavenDBLevel = null, string logPath = null, bool logToConsole = true)
        {
            LoggingLevel = InitializeLevel("LogLevel", defaultLevel ?? LogLevel.Info);
            RavenDBLogLevel = InitializeLevel("RavenDBLogLevel", defaultRavenDBLevel ?? LogLevel.Warn);
            LogPath = Environment.ExpandEnvironmentVariables(SettingsReader<string>.Read("LogPath", logPath ?? DefaultLogPathForInstance(serviceName)));
            LogToConsole = logToConsole;
        }

        public LogLevel LoggingLevel { get; }

        public LogLevel RavenDBLogLevel { get; }

        public string LogPath { get; }

        public bool LogToConsole { get; }

        LogLevel InitializeLevel(string key, LogLevel defaultLevel)
        {
            var levelText = SettingsReader<string>.Read("ServiceControl", key);
            if (!string.IsNullOrWhiteSpace(levelText))
            {
                return defaultLevel;
            }

            try
            {
                return LogLevel.FromString(levelText);
            }
            catch
            {
                InternalLogger.Warn($"Failed to parse {key} setting. Defaulting to {defaultLevel.Name}.");
                return defaultLevel;
            }
        }

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