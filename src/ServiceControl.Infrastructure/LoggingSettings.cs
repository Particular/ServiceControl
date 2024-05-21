namespace ServiceControl.Infrastructure;

using System;
using System.IO;
using NLog;
using NLog.Common;
using ServiceControl.Configuration;

public class LoggingSettings(SettingsRootNamespace rootNamespace, LogLevel defaultLevel = null, string logPath = null)
{
    public LogLevel LogLevel { get; } = InitializeLogLevel(rootNamespace, defaultLevel);

    public string LogPath { get; } = SettingsReader.Read(rootNamespace, "LogPath", Environment.ExpandEnvironmentVariables(logPath ?? DefaultLogLocation()));

    static LogLevel InitializeLogLevel(SettingsRootNamespace rootNamespace, LogLevel defaultLevel)
    {
        defaultLevel ??= LogLevel.Info;

        var levelText = SettingsReader.Read<string>(rootNamespace, logLevelKey);

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