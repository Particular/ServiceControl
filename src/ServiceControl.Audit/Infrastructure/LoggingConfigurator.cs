namespace ServiceControl.Audit.Infrastructure
{
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using NLog.Config;
    using NLog.Layouts;
    using NLog.Targets;
    using NServiceBus.Extensions.Logging;
    using LogManager = NServiceBus.Logging.LogManager;
    using Settings;
    using LogLevel = NLog.LogLevel;
    using NLog.Extensions.Logging;
    using NLog;
    using System;

    static class LoggingConfigurator
    {
        public static void ConfigureLogging(LoggingSettings loggingSettings)
        {
            if (NLog.LogManager.Configuration != null)
            {
                return;
            }

            var version = FileVersionInfo.GetVersionInfo(typeof(Bootstrapper).Assembly.Location).ProductVersion;
            var nlogConfig = new LoggingConfiguration();
            var simpleLayout = new SimpleLayout("${longdate}|${threadid}|${level}|${logger}|${message}${onexception:|${exception:format=tostring}}");
            var header = $@"-------------------------------------------------------------
ServiceControl Audit Version:				{version}
-------------------------------------------------------------";

            var fileTarget = new FileTarget
            {
                ArchiveEvery = FileArchivePeriod.Day,
                FileName = Path.Combine(loggingSettings.LogPath, "logfile.${shortdate}.txt"),
                ArchiveFileName = Path.Combine(loggingSettings.LogPath, "logfile.{#}.txt"),
                ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
                Layout = simpleLayout,
                MaxArchiveFiles = 14,
                ArchiveAboveSize = 30 * MegaByte
            };

            var ravenFileTarget = new FileTarget
            {
                ArchiveEvery = FileArchivePeriod.Day,
                FileName = Path.Combine(loggingSettings.LogPath, "ravenlog.${shortdate}.txt"),
                ArchiveFileName = Path.Combine(loggingSettings.LogPath, "ravenlog.{#}.txt"),
                ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
                Layout = simpleLayout,
                MaxArchiveFiles = 14,
                ArchiveAboveSize = 30 * MegaByte
            };

            var consoleTarget = new ColoredConsoleTarget
            {
                Layout = simpleLayout,
                UseDefaultRowHighlightingRules = true
            };

            var nullTarget = new NullTarget();

            nlogConfig.AddTarget("console", consoleTarget);
            nlogConfig.AddTarget("debugger", fileTarget);
            nlogConfig.AddTarget("raven", ravenFileTarget);
            nlogConfig.AddTarget("null", nullTarget);

            // Only want to see raven errors
            nlogConfig.LoggingRules.Add(new LoggingRule("Raven.*", loggingSettings.RavenDBLogLevel, ravenFileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("Raven.*", LogLevel.Error, consoleTarget)); //Noise reduction - Only RavenDB errors on the console
            nlogConfig.LoggingRules.Add(new LoggingRule("Raven.*", LogLevel.Debug, nullTarget)
            {
                Final = true
            }); //Will swallow debug and above messages

            // Always want to see license logging regardless of default logging level
            nlogConfig.LoggingRules.Add(new LoggingRule("Particular.ServiceControl.Licensing.*", LogLevel.Info, fileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("Particular.ServiceControl.Licensing.*", LogLevel.Info, consoleTarget)
            {
                Final = true
            });


            // Defaults
            nlogConfig.LoggingRules.Add(new LoggingRule("*", loggingSettings.LoggingLevel, fileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("*", loggingSettings.LoggingLevel < LogLevel.Info ? loggingSettings.LoggingLevel : LogLevel.Info, consoleTarget));


            if (!loggingSettings.LogToConsole)
            {
                foreach (var rule in nlogConfig.LoggingRules.Where(p => p.Targets.Contains(consoleTarget)).ToList())
                {
                    nlogConfig.LoggingRules.Remove(rule);
                }
            }

            NLog.LogManager.Configuration = nlogConfig;

            LogManager.UseFactory(new ExtensionsLoggerFactory(new NLogLoggerFactory()));

            var logger = LogManager.GetLogger("LoggingConfiguration");
            var logEventInfo = new LogEventInfo
            {
                TimeStamp = DateTime.UtcNow
            };
            logger.InfoFormat("Logging to {0} with LogLevel '{1}'", fileTarget.FileName.Render(logEventInfo), loggingSettings.LoggingLevel.Name);
        }

        const long MegaByte = 1024 * 1024;
    }
}