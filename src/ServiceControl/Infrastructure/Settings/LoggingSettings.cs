namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.IO;
    using NLog;
    using NLog.Common;
    using ServiceControl.Configuration;

    public class LoggingSettings(LogLevel defaultLevel = null, string logPath = null)
    {
        public LogLevel LoggingLevel { get; } = InitializeLogLevel(defaultLevel);

        public string LogPath { get; } = SettingsReader.Read(Settings.SettingsRootNamespace, "LogPath", Environment.ExpandEnvironmentVariables(logPath ?? DefaultLogLocation()));

        static LogLevel InitializeLogLevel(LogLevel defaultLevel)
        {
            defaultLevel ??= LogLevel.Info;

            var levelText = SettingsReader.Read<string>(Settings.SettingsRootNamespace, logLevelKey);

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
                InternalLogger.Warn($"Failed to parse {logLevelKey} setting. Defaulting to {defaultLevel.Name}.");
                return defaultLevel;
            }
        }

        // SC installer always populates LogPath in app.config on installation/change/upgrade so this will only be used when
        // debugging or if the entry is removed manually. In those circumstances default to the folder containing the exe
        static string DefaultLogLocation() => Path.Combine(AppContext.BaseDirectory, ".logs");

        public Microsoft.Extensions.Logging.LogLevel ToHostLogLevel() => LoggingLevel switch
        {
            _ when LoggingLevel == LogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
            _ when LoggingLevel == LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            _ when LoggingLevel == LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
            _ when LoggingLevel == LogLevel.Warn => Microsoft.Extensions.Logging.LogLevel.Warning,
            _ when LoggingLevel == LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            _ when LoggingLevel == LogLevel.Fatal => Microsoft.Extensions.Logging.LogLevel.Critical,
            _ => Microsoft.Extensions.Logging.LogLevel.None
        };

        const string logLevelKey = "LogLevel";
    }
}