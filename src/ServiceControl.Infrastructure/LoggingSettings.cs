namespace ServiceControl.Infrastructure;

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public record LoggingOptions
{
    public string LogLevel { get; set; }
    public string LogPath { get; set; }
    public string LoggingProviders { get; set; }
    public string SeqAddress { get; set; }
}

public static class LoggingOptionsToSettings
{
    public static LoggingSettings Map(LoggingOptions src)
    {
        LoggingSettings dst = new();

        var activeLoggers = Loggers.None;
        if (src.LoggingProviders.Contains("NLog"))
        {
            activeLoggers |= Loggers.NLog;
        }

        if (src.LoggingProviders.Contains("Seq"))
        {
            activeLoggers |= Loggers.Seq;
            if (!string.IsNullOrWhiteSpace(src.SeqAddress))
            {
                LoggerUtil.SeqAddress = src.SeqAddress;
            }
        }

        if (src.LoggingProviders.Contains("Otlp"))
        {
            activeLoggers |= Loggers.Otlp;
        }

        //this defaults to NLog because historically that was the default, and we don't want to break existing installs that don't have the config key to define loggingProviders
        LoggerUtil.ActiveLoggers = activeLoggers == Loggers.None ? Loggers.NLog : activeLoggers;

        dst.LogLevel = InitializeLogLevel(src.LogLevel, dst.LogLevel);
        dst.LogPath = Environment.ExpandEnvironmentVariables(src.LogPath ?? DefaultLogLocation());

        static LogLevel InitializeLogLevel(string levelText, LogLevel defaultLevel)
        {
            if (string.IsNullOrWhiteSpace(levelText))
            {
                return defaultLevel;
            }

            return ParseLogLevel(levelText, defaultLevel);
        }

        return dst;
    }

    // SC installer always populates LogPath in app.config on installation/change/upgrade so this will only be used when
    // debugging or if the entry is removed manually. In those circumstances default to the folder containing the exe
    static string DefaultLogLocation() => Path.Combine(AppContext.BaseDirectory, ".logs");

    // This is not a complete mapping of NLog levels, just the ones that are different.
    static readonly Dictionary<string, LogLevel> NLogAliases = new(StringComparer.OrdinalIgnoreCase)
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

        LoggerUtil.CreateStaticLogger<LoggingSettings>().LogWarning("Failed to parse {LogLevelKey} setting. Defaulting to {DefaultLevel}", nameof(LoggingOptions.LogLevel), defaultLevel);

        return defaultLevel;
    }
}


public class LoggingSettings // TODO: Register
{
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
    public string LogPath { get; set; }
}
