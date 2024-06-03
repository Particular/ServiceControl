namespace ServiceControl.Infrastructure
{
    using System;
    using System.IO;
    using NLog;
    using NLog.Config;
    using NLog.Extensions.Logging;
    using NLog.Layouts;
    using NLog.Targets;
    using NServiceBus.Extensions.Logging;
    using ServiceControl.Configuration;
    using LogLevel = NLog.LogLevel;
    using LogManager = NServiceBus.Logging.LogManager;

    public static class LoggingConfigurator
    {
        public static void ConfigureLogging(LoggingSettings loggingSettings)
        {
            if (NLog.LogManager.Configuration != null)
            {
                return;
            }

            var nlogConfig = new LoggingConfiguration();
            var simpleLayout = new SimpleLayout("${longdate}|${threadname:whenEmpty=${threadid}}|${level}|${logger}|${message}${onexception:|${exception:format=message}}");

            var fileTarget = new FileTarget
            {
                Name = "file",
                ArchiveEvery = FileArchivePeriod.Day,
                FileName = Path.Combine(loggingSettings.LogPath, "logfile.${shortdate}.txt"),
                ArchiveFileName = Path.Combine(loggingSettings.LogPath, "logfile.{#}.txt"),
                ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
                Layout = simpleLayout,
                MaxArchiveFiles = 14,
                ArchiveAboveSize = 30 * megaByte
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
            nlogConfig.LoggingRules.Add(new LoggingRule("Particular.ServiceControl.Licensing.*", LogLevel.Info, consoleTarget));
            // Defaults
            nlogConfig.LoggingRules.Add(new LoggingRule("*", loggingSettings.LogLevel < LogLevel.Info ? loggingSettings.LogLevel : LogLevel.Info, consoleTarget));

            if (!AppEnvironment.RunningInContainer)
            {
                nlogConfig.LoggingRules.Add(new LoggingRule("Particular.ServiceControl.Licensing.*", LogLevel.Info, fileTarget));
                nlogConfig.LoggingRules.Add(new LoggingRule("*", loggingSettings.LogLevel, fileTarget));
            }

            NLog.LogManager.Configuration = nlogConfig;

            LogManager.UseFactory(new ExtensionsLoggerFactory(new NLogLoggerFactory()));

            var logger = LogManager.GetLogger("LoggingConfiguration");
            var logEventInfo = new LogEventInfo { TimeStamp = DateTime.UtcNow };
            var loggingTo = AppEnvironment.RunningInContainer ? "console" : fileTarget.FileName.Render(logEventInfo);
            logger.InfoFormat("Logging to {0} with LogLevel '{1}'", loggingTo, loggingSettings.LogLevel.Name);
        }

        const long megaByte = 1024 * 1024;
    }
}