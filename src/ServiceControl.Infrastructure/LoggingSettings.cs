namespace ServiceControl.Infrastructure;

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using ServiceControl.Configuration;

public class LoggingSettings(SettingsRootNamespace rootNamespace, LogLevel defaultLevel = LogLevel.Information, string logPath = null)
{
    public LogLevel LogLevel { get; } = InitializeLogLevel(rootNamespace, defaultLevel);

    public string LogPath { get; } = SettingsReader.Read(rootNamespace, logPathKey, Environment.ExpandEnvironmentVariables(logPath ?? DefaultLogLocation()));

    static LogLevel InitializeLogLevel(SettingsRootNamespace rootNamespace, LogLevel defaultLevel)
    {
        var levelText = SettingsReader.Read<string>(rootNamespace, logLevelKey);

        if (string.IsNullOrWhiteSpace(levelText))
        {
            return defaultLevel;
        }

        return ParseLogLevel(levelText, defaultLevel);
    }

    // SC installer always populates LogPath in app.config on installation/change/upgrade so this will only be used when
    // debugging or if the entry is removed manually. In those circumstances default to the folder containing the exe
    static string DefaultLogLocation() => Path.Combine(AppContext.BaseDirectory, ".logs");

    // This is not a complete mapping of NLog levels, just the ones that are different.
    static readonly Dictionary<string, LogLevel> NLogAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["info"] = LogLevel.Information,
            ["warn"] = LogLevel.Warning,
            ["fatal"] = LogLevel.Critical,
            ["off"] = LogLevel.None
        };

    static LogLevel ParseLogLevel(string value, LogLevel defaultLevel)
    {
        if (Enum.TryParse(value, ignoreCase: true, out LogLevel parsedLevel))
        {
            return parsedLevel;
        }

        if (NLogAliases.TryGetValue(value.Trim(), out parsedLevel))
        {
            return parsedLevel;
        }

        LoggerUtil.CreateStaticLogger<LoggingSettings>().LogWarning("Failed to parse {LogLevelKey} setting. Defaulting to {DefaultLevel}.", logLevelKey, defaultLevel);

        return defaultLevel;
    }

    const string logLevelKey = "LogLevel";
    const string logPathKey = "LogPath";
}