namespace ServiceControl.Infrastructure
{
    using System;
    using System.IO;
    using NLog;
    using NLog.Config;
    using NLog.Layouts;
    using NLog.Targets;
    using ServiceControl.Configuration;
    using ServiceControl.Infrastructure.Auth;
    using LogManager = NServiceBus.Logging.LogManager;
    using LogLevel = NLog.LogLevel;

    public static class LoggingConfigurator
    {
        public static void ConfigureLogging(LoggingSettings loggingSettings)
        {
            if (!LoggerUtil.IsLoggingTo(Loggers.NLog) || NLog.LogManager.Configuration != null)
            {
                return;
            }

            var logLevel = loggingSettings.LogLevel.ToNLogLevel();
            var loggingTo = ConfigureNLog("logfile.txt", loggingSettings.LogPath, loggingSettings.LogLevel.ToNLogLevel());

            //using LogManager here rather than LoggerUtil.CreateStaticLogger since this is exclusive to NLog
            var logger = LogManager.GetLogger("LoggingConfiguration");
            logger.InfoFormat("Logging to {0} with LogLevel '{1}'", loggingTo, logLevel.Name);
        }

        public static string ConfigureNLog(string logFileName, string logPath, LogLevel logLevel)
        {
            var nlogConfig = BuildConfiguration(logFileName, logPath, logLevel);

            NLog.LogManager.Configuration = nlogConfig;

            var logEventInfo = new LogEventInfo { TimeStamp = DateTime.UtcNow };
            var fileTarget = nlogConfig.FindTargetByName<FileTarget>("file");
            return AppEnvironment.RunningInContainer ? "console" : fileTarget.FileName.Render(logEventInfo);
        }

        public static LoggingConfiguration BuildConfiguration(string logFileName, string logPath, LogLevel logLevel)
        {
            //configure NLog
            var nlogConfig = new LoggingConfiguration();
            var simpleLayout = new SimpleLayout("${longdate}|${processtime}|${threadid}|${level}|${logger}|${message}${onexception:|${exception:format=tostring}}");

            var fileTarget = new FileTarget
            {
                Name = "file",
                ArchiveEvery = FileArchivePeriod.Day,
                FileName = Path.Combine(logPath, logFileName),
                ArchiveSuffixFormat = ".{1:yyyy-MM-dd}.{0:00}",
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

            // The authorization audit trail is emitted on a dedicated category as structured JSON so it can be
            // shipped to a SIEM without being polluted by — or polluting — the plain-text operational log.
            var auditLayout = new JsonLayout
            {
                IncludeEventProperties = true, // SubjectId, SubjectName, Permission, Resource, Reason, …
                Attributes =
                {
                    new JsonAttribute("timestamp", "${longdate:universalTime=true}"),
                    new JsonAttribute("level", "${level:uppercase=true}"),
                    new JsonAttribute("category", "${logger}"),
                    new JsonAttribute("message", "${message}")
                }
            };

            var auditConsoleTarget = new ConsoleTarget
            {
                Name = "audit-console",
                Layout = auditLayout
            };

            var auditFileTarget = new FileTarget
            {
                Name = "audit-file",
                ArchiveEvery = FileArchivePeriod.Day,
                FileName = Path.Combine(logPath, "audit.json"),
                ArchiveSuffixFormat = ".{1:yyyy-MM-dd}.{0:00}",
                Layout = auditLayout,
                MaxArchiveFiles = 14,
                ArchiveAboveSize = 30 * megaByte
            };

            // Audit events are captured from Info upward (allow = Information, deny = Warning) regardless of the
            // operational LogLevel — lowering the operational verbosity must never drop entries from the audit trail.
            // Final stops audit events from also reaching the catch-all operational rules below, so this rule must
            // be registered before them.
            var auditRule = new LoggingRule
            {
                LoggerNamePattern = $"{AuthorizationAuditLog.AuditCategory}*",
                Final = true
            };
            auditRule.SetLoggingLevels(LogLevel.Info, LogLevel.Fatal);
            auditRule.Targets.Add(auditConsoleTarget);
            if (!AppEnvironment.RunningInContainer)
            {
                auditRule.Targets.Add(auditFileTarget);
            }

            nlogConfig.AddTarget(consoleTarget);
            nlogConfig.AddTarget(auditConsoleTarget);

            nlogConfig.LoggingRules.Add(aspNetCoreRule);
            nlogConfig.LoggingRules.Add(httpClientRule);
            nlogConfig.LoggingRules.Add(auditRule);

            nlogConfig.LoggingRules.Add(new LoggingRule("*", logLevel, consoleTarget));

            if (!AppEnvironment.RunningInContainer)
            {
                nlogConfig.AddTarget(fileTarget);
                nlogConfig.AddTarget(auditFileTarget);
                nlogConfig.LoggingRules.Add(new LoggingRule("*", logLevel, fileTarget));
            }

            return nlogConfig;
        }

        static LogLevel ToNLogLevel(this Microsoft.Extensions.Logging.LogLevel level) =>
            level switch
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

        const long megaByte = 1024 * 1024;

    }
}