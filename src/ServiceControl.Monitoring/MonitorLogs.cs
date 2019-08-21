﻿namespace ServiceControl.Monitoring
{
    using System;
    using System.IO;
    using System.Linq;
    using NLog;
    using NLog.Common;
    using NLog.Config;
    using NLog.Layouts;
    using NLog.Targets;
    using NServiceBus;

    static class MonitorLogs
    {
        const long MegaByte = 1073741824;

        const string LogLevelKey = "Monitoring/LogLevel";

        public static void Configure(Settings settings)
        {
            NServiceBus.Logging.LogManager.Use<NLogFactory>();

            if (LogManager.Configuration != null)
            {
                return;
            }

            var version = typeof(HostService).Assembly.GetName().Version;
            var nlogConfig = new LoggingConfiguration();
            var simpleLayout = new SimpleLayout("${longdate}|${threadid}|${level}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}");
            var header = $@"-------------------------------------------------------------
ServiceControl Monitoring Version:				{version}
Selected Transport:					{settings.TransportType}
-------------------------------------------------------------";

            var fileTarget = new FileTarget
            {
                ArchiveEvery = FileArchivePeriod.Day,
                FileName = Path.Combine(settings.LogPath, "logfile.${shortdate}.txt"),
                ArchiveFileName = Path.Combine(settings.LogPath, "logfile.{#}.txt"),
                ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
                Layout = simpleLayout,
                MaxArchiveFiles = 14,
                ArchiveAboveSize = 30 * MegaByte,
                Header = new SimpleLayout(header)
            };

            var consoleTarget = new ColoredConsoleTarget
            {
                Layout = simpleLayout,
                UseDefaultRowHighlightingRules = true
            };

            var nullTarget = new NullTarget();

            nlogConfig.AddTarget("console", consoleTarget);
            nlogConfig.AddTarget("debugger", fileTarget);
            nlogConfig.AddTarget("null", nullTarget);
            
            //Suppress NSB license logging since this will have it's own
            nlogConfig.LoggingRules.Add(new LoggingRule("NServiceBus.LicenseManager", LogLevel.Info, nullTarget) { Final = true });

            // Always want to see license logging regardless of default logging level
            nlogConfig.LoggingRules.Add(new LoggingRule("ServiceControl.Monitoring.Licensing.*", LogLevel.Info, fileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("ServiceControl.Monitoring.Licensing.*", LogLevel.Info, consoleTarget)
            {
                Final = true
            });
            
            // Defaults
            nlogConfig.LoggingRules.Add(new LoggingRule("*", settings.LogLevel, fileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("*", settings.LogLevel < LogLevel.Info ? settings.LogLevel : LogLevel.Info, consoleTarget));

            // Remove Console Logging when running as a service
            if (!Environment.UserInteractive)
            {
                foreach (var rule in nlogConfig.LoggingRules.Where(p => p.Targets.Contains(consoleTarget)).ToList())
                {
                    nlogConfig.LoggingRules.Remove(rule);
                }
            }

            LogManager.Configuration = nlogConfig;

            var logger = NServiceBus.Logging.LogManager.GetLogger("LoggingConfiguration");
            var logEventInfo = new LogEventInfo
            {
                TimeStamp = DateTime.Now
            };
            logger.InfoFormat("Logging to {0} with LogLevel '{1}'", fileTarget.FileName.Render(logEventInfo), settings.LogLevel.Name);
        }

        public static LogLevel InitializeLevel(SettingsReader reader)
        {
            var level = LogLevel.Warn;
            try
            {
                level = LogLevel.FromString(reader.Read(LogLevelKey, LogLevel.Warn.Name));
            }
            catch
            {
                InternalLogger.Warn($"Failed to parse {LogLevelKey} setting. Defaulting to Warn.");
            }
            return level;
        }
    }
}
