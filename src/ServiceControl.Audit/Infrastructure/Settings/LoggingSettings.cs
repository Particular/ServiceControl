namespace ServiceControl.Audit.Infrastructure.Settings
{
    using System;
    using System.IO;
    using Configuration;
    using NLog;
    using NLog.Common;

    public class LoggingSettings(LogLevel defaultLevel = null, string logPath = null)
    {
        public LogLevel LogLevel { get; } = InitializeLogLevel(defaultLevel);

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

        public Microsoft.Extensions.Logging.LogLevel ToHostLogLevel() => LogLevel switch
        {
            _ when LogLevel == LogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
            _ when LogLevel == LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            _ when LogLevel == LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
            _ when LogLevel == LogLevel.Warn => Microsoft.Extensions.Logging.LogLevel.Warning,
            _ when LogLevel == LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            _ when LogLevel == LogLevel.Fatal => Microsoft.Extensions.Logging.LogLevel.Critical,
            _ => Microsoft.Extensions.Logging.LogLevel.None
        };

        const string logLevelKey = "LogLevel";
    }
}