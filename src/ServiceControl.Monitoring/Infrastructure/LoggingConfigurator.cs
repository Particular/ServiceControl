namespace ServiceControl.Monitoring
{
    using System;
    using System.IO;
    using Configuration;
    using NLog;
    using NLog.Common;
    using NLog.Config;
    using NLog.Extensions.Logging;
    using NLog.Layouts;
    using NLog.Targets;
    using NServiceBus.Extensions.Logging;
    using LogManager = NServiceBus.Logging.LogManager;

    static class LoggingConfigurator
    {
        public static void Configure(Settings settings)
        {
            if (NLog.LogManager.Configuration != null)
            {
                return;
            }

            var nlogConfig = new LoggingConfiguration();
            var simpleLayout = new SimpleLayout("${longdate}|${threadid}|${level}|${logger}|${message}${onexception:|${exception:format=tostring}}");

            var fileTarget = new FileTarget
            {
                Name = "file",
                ArchiveEvery = FileArchivePeriod.Day,
                FileName = Path.Combine(settings.LogPath, "logfile.${shortdate}.txt"),
                ArchiveFileName = Path.Combine(settings.LogPath, "logfile.{#}.txt"),
                ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
                Layout = simpleLayout,
                MaxArchiveFiles = 14,
                ArchiveAboveSize = 30 * megaByte,
            };

            var consoleTarget = new ColoredConsoleTarget
            {
                Name = "console",
                Layout = simpleLayout,
                DetectConsoleAvailable = true,
                DetectOutputRedirected = true,
                UseDefaultRowHighlightingRules = true
            };

            // Always want to see license logging regardless of default logging level
            nlogConfig.LoggingRules.Add(new LoggingRule("ServiceControl.Monitoring.Licensing.*", LogLevel.Info, fileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("ServiceControl.Monitoring.Licensing.*", LogLevel.Info, consoleTarget));

            // Defaults
            nlogConfig.LoggingRules.Add(new LoggingRule("*", settings.LogLevel, fileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("*", settings.LogLevel < LogLevel.Info ? settings.LogLevel : LogLevel.Info, consoleTarget));

            NLog.LogManager.Configuration = nlogConfig;

            LogManager.UseFactory(new ExtensionsLoggerFactory(new NLogLoggerFactory()));

            var logger = LogManager.GetLogger("LoggingConfiguration");
            var logEventInfo = new LogEventInfo { TimeStamp = DateTime.UtcNow };
            logger.InfoFormat("Logging to {0} with LogLevel '{1}'", fileTarget.FileName.Render(logEventInfo), settings.LogLevel.Name);
        }

        public static LogLevel InitializeLevel()
        {
            var level = LogLevel.Info;
            try
            {
                level = LogLevel.FromString(SettingsReader.Read(Settings.SettingsRootNamespace, LogLevelKey, LogLevel.Info.Name));
            }
            catch
            {
                InternalLogger.Warn($"Failed to parse {LogLevelKey} setting. Defaulting to Warn.");
            }

            return level;
        }

        const long megaByte = 1024 * 1024;

        const string LogLevelKey = "LogLevel";
    }
}