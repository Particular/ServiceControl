namespace ServiceControl.Audit.Infrastructure.Settings
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
            var levelText = SettingsReader<string>.Read(key);
            if (string.IsNullOrWhiteSpace(levelText))
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

        static string DefaultLogPathForInstance(string serviceName)
        {
            if (serviceName.Equals(Settings.DEFAULT_SERVICE_NAME, StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Particular\\ServiceControl.Audit\\logs");
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), $"Particular\\{serviceName}\\logs");
        }

        public Microsoft.Extensions.Logging.LogLevel ToHostLogLevel()
        {
            if (LoggingLevel == LogLevel.Debug)
            {
                return Microsoft.Extensions.Logging.LogLevel.Debug;
            }
            if (LoggingLevel == LogLevel.Error)
            {
                return Microsoft.Extensions.Logging.LogLevel.Error;
            }
            if (LoggingLevel == LogLevel.Fatal)
            {
                return Microsoft.Extensions.Logging.LogLevel.Critical;
            }
            if (LoggingLevel == LogLevel.Warn)
            {
                return Microsoft.Extensions.Logging.LogLevel.Warning;
            }
            if (LoggingLevel == LogLevel.Info)
            {
                return Microsoft.Extensions.Logging.LogLevel.Information;
            }
            if (LoggingLevel == LogLevel.Trace)
            {
                return Microsoft.Extensions.Logging.LogLevel.Trace;
            }

            return Microsoft.Extensions.Logging.LogLevel.None;
        }
    }
}