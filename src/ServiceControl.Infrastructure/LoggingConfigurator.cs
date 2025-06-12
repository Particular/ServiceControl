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
            var simpleLayout = new SimpleLayout("${longdate}|${processtime}|${threadid}|${level}|${logger}|${message}${onexception:|${exception:format=tostring}}");

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

            var aspNetCoreRule = new LoggingRule()
            {
                LoggerNamePattern = "Microsoft.AspNetCore.*",
                FinalMinLevel = LogLevel.Warn
            };

            var httpClientRule = new LoggingRule()
            {
                LoggerNamePattern = "System.Net.Http.HttpClient.*",
                FinalMinLevel = LogLevel.Warn
            };

            nlogConfig.LoggingRules.Add(aspNetCoreRule);
            nlogConfig.LoggingRules.Add(httpClientRule);

            // HACK: Fix LogLevel to Info for testing purposes only.
            // nlogConfig.LoggingRules.Add(new LoggingRule("*", loggingSettings.LogLevel, consoleTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, consoleTarget));

            if (!AppEnvironment.RunningInContainer)
            {
                // HACK: Fix LogLevel to Info for testing purposes only.
                //nlogConfig.LoggingRules.Add(new LoggingRule("*", loggingSettings.LogLevel, fileTarget));
                nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, fileTarget));
            }

            NLog.LogManager.Configuration = nlogConfig;

            LogManager.UseFactory(new ExtensionsLoggerFactory(new NLogLoggerFactory()));

            var logger = LogManager.GetLogger("LoggingConfiguration");
            var logEventInfo = new LogEventInfo { TimeStamp = DateTime.UtcNow };
            var loggingTo = AppEnvironment.RunningInContainer ? "console" : fileTarget.FileName.Render(logEventInfo);
            logger.InfoFormat("Logging to {0} with LogLevel '{1}'", loggingTo, LogLevel.Info.Name);
        }

        const long megaByte = 1024 * 1024;
    }
}