namespace Particular.ServiceControl
{
    using System.IO;
    using System.Linq;
    using NLog.Config;
    using NLog.Layouts;
    using NLog.Targets;
    using NServiceBus;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;
    using LogLevel = NLog.LogLevel;

    static class LoggingConfigurator
    {
        public static void ConfigureLogging(LoggingSettings loggingSettings)
        {
            LogManager.Use<NLogFactory>();

            const long megaByte = 1024 * 1024;
            if (NLog.LogManager.Configuration != null)
            {
                return;
            }

            var nlogConfig = new LoggingConfiguration();
            var simpleLayout = new SimpleLayout("${longdate}|${threadid}|${level}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}");

            var fileTarget = new FileTarget
            {
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
                Layout = simpleLayout,
                UseDefaultRowHighlightingRules = true
            };

            var nullTarget = new NullTarget();

            // There lines don't appear to be necessary.  The rules seem to work without implicitly adding the targets?!?
            nlogConfig.AddTarget("console", consoleTarget);
            nlogConfig.AddTarget("debugger", fileTarget);
            nlogConfig.AddTarget("bitbucket", nullTarget);

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
        }
    }
}