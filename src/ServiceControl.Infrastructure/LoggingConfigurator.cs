namespace ServiceControl.Infrastructure
{
    using System;
    using System.IO;
    using Microsoft.Extensions.Logging;
    using NLog;
    using NLog.Config;
    using NLog.Layouts;
    using NLog.Targets;
    using NServiceBus.Extensions.Logging;
    using ServiceControl.Configuration;
    using LogManager = NServiceBus.Logging.LogManager;
    using LogLevel = NLog.LogLevel;

    public static class LoggingConfigurator
    {
        public static void ConfigureLogging(LoggingSettings loggingSettings)
        {
            //used for loggers outside of ServiceControl (i.e. transports and core) to use the logger factory defined here
            LogManager.UseFactory(new ExtensionsLoggerFactory(LoggerFactory.Create(configure => configure.ConfigureLogging(loggingSettings.LogLevel))));

            if (!LoggerUtil.IsLoggingTo(Loggers.NLog) || NLog.LogManager.Configuration != null)
            {
                return;
            }

            var logLevel = loggingSettings.LogLevel.ToNLogLevel();
            var loggingTo = ConfigureNLog("logfile.${shortdate}.txt", loggingSettings.LogPath, loggingSettings.LogLevel.ToNLogLevel());

            //using LogManager here rather than LoggerUtil.CreateStaticLogger since this is exclusive to NLog
            var logger = LogManager.GetLogger("LoggingConfiguration");
            logger.InfoFormat("Logging to {0} with LogLevel '{1}'", loggingTo, logLevel.Name);
        }

        public static string ConfigureNLog(string logFileName, string logPath, LogLevel logLevel)
        {
            //configure NLog
            var nlogConfig = new LoggingConfiguration();
            var simpleLayout = new SimpleLayout("${longdate}|${processtime}|${threadid}|${level}|${logger}|${message}${onexception:|${exception:format=tostring}}");

            var fileTarget = new FileTarget
            {
                Name = "file",
                ArchiveEvery = FileArchivePeriod.Day,
                FileName = Path.Combine(logPath, logFileName),
                ArchiveFileName = Path.Combine(logPath, "logfile.{#}.txt"),
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

            var efCoreRule = new LoggingRule()
            {
                LoggerNamePattern = "Microsoft.EntityFrameworkCore.*",
                FinalMinLevel = LogLevel.Warn
            };

            nlogConfig.LoggingRules.Add(aspNetCoreRule);
            nlogConfig.LoggingRules.Add(httpClientRule);
            nlogConfig.LoggingRules.Add(efCoreRule);

            nlogConfig.LoggingRules.Add(new LoggingRule("*", logLevel, consoleTarget));

            if (!AppEnvironment.RunningInContainer)
            {
                nlogConfig.LoggingRules.Add(new LoggingRule("*", logLevel, fileTarget));
            }

            NLog.LogManager.Configuration = nlogConfig;

            var logEventInfo = new LogEventInfo { TimeStamp = DateTime.UtcNow };
            return AppEnvironment.RunningInContainer ? "console" : fileTarget.FileName.Render(logEventInfo);
        }

        static LogLevel ToNLogLevel(this Microsoft.Extensions.Logging.LogLevel level)
        {
            return level switch
            {
                Microsoft.Extensions.Logging.LogLevel.Trace => LogLevel.Trace,
                Microsoft.Extensions.Logging.LogLevel.Debug => LogLevel.Debug,
                Microsoft.Extensions.Logging.LogLevel.Information => LogLevel.Info,
                Microsoft.Extensions.Logging.LogLevel.Warning => LogLevel.Warn,
                Microsoft.Extensions.Logging.LogLevel.Error => LogLevel.Error,
                Microsoft.Extensions.Logging.LogLevel.Critical => LogLevel.Fatal,
                Microsoft.Extensions.Logging.LogLevel.None => LogLevel.Off,
                _ => LogLevel.Off,
            };
        }

        const long megaByte = 1024 * 1024;

    }
}