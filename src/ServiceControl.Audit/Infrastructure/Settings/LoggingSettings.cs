namespace ServiceControl.Audit.Infrastructure.Settings
{
    using System;
    using System.IO;
    using System.Reflection;
    using Configuration;
    using NLog;
    using NLog.Common;

    public class LoggingSettings
    {
        public LoggingSettings(LogLevel defaultLevel = null, string logPath = null)
        {
            LoggingLevel = InitializeLevel("LogLevel", defaultLevel ?? LogLevel.Info);
            LogPath = SettingsReader.Read(Settings.SettingsRootNamespace, "LogPath", Environment.ExpandEnvironmentVariables(logPath ?? DefaultLogLocation()));
        }

        public LogLevel LoggingLevel { get; }

        public string LogPath { get; }

        LogLevel InitializeLevel(string key, LogLevel defaultLevel)
        {
            var levelText = SettingsReader.Read<string>(Settings.SettingsRootNamespace, key);
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

        // SC installer always populates LogPath in app.config on installation/change/upgrade so this will only be used when
        // debugging or if the entry is removed manually. In those circumstances default to the folder containing the exe
        static string DefaultLogLocation()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            return Path.Combine(Path.GetDirectoryName(assemblyLocation), ".logs");
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