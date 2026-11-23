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
        public static void ConfigureLogging(LoggingSettings loggingSettings, string logFileName = "logfile.txt", bool logToConsole = true)
        {
            if (!LoggerUtil.IsLoggingTo(Loggers.NLog) || NLog.LogManager.Configuration != null)
            {
                return;
            }

            var logLevel = loggingSettings.LogLevel.ToNLogLevel();
            var loggingTo = ConfigureNLog(logFileName, loggingSettings.LogPath, loggingSettings.LogLevel.ToNLogLevel(), logToConsole);

            if (logToConsole || AppEnvironment.RunningInContainer)
            {
                // Uses LogManager rather than LoggerUtil.CreateStaticLogger, because this line is about NLog alone.
                var logger = LogManager.GetLogger("LoggingConfiguration");
                logger.InfoFormat("Logging to {0} with LogLevel '{1}'", loggingTo, logLevel.Name);
            }
        }

        public static string ConfigureNLog(string logFileName, string logPath, LogLevel logLevel, bool logToConsole = true)
        {
            var nlogConfig = BuildConfiguration(logFileName, logPath, logLevel, logToConsole);

            NLog.LogManager.Configuration = nlogConfig;

            var logEventInfo = new LogEventInfo { TimeStamp = DateTime.UtcNow };
            var fileTarget = nlogConfig.FindTargetByName<FileTarget>("file");
            return AppEnvironment.RunningInContainer ? "console" : fileTarget.FileName.Render(logEventInfo);
        }

        public static string MigrationLogFileName(DateTime localStart) => $"migration-{localStart:yyyy-MM-dd-HHmmss}.txt";

        public static string CreateMigrationLogFile(string logPath, DateTime localStart, bool runningInContainer)
        {
            var name = MigrationLogFileName(localStart);

            if (runningInContainer)
            {
                return name;
            }

            try
            {
                Directory.CreateDirectory(logPath);

                using (new FileStream(Path.Combine(logPath, name), FileMode.Append, FileAccess.Write))
                {
                }

                return name;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new Exception($"The log folder '{logPath}' cannot be written, so the migration did not start. Set ServiceControl/LogPath to a folder this account can write to.", e);
            }
        }

        public static string CurrentLogFile() =>
            NLog.LogManager.Configuration?.FindTargetByName<FileTarget>("file")?.FileName.Render(LogEventInfo.CreateNullEvent());

        public static LoggingConfiguration BuildConfiguration(string logFileName, string logPath, LogLevel logLevel, bool logToConsole = true)
        {
            //configure NLog
            var nlogConfig = new LoggingConfiguration();
            var simpleLayout = new SimpleLayout("${longdate}|${processtime}|${threadid}|${level}|${logger}|${message}${onexception:|${exception:format=tostring}}");

            // A run that logs only to its own file must keep one file, or the path printed at the end holds only the tail.
            var archive = logToConsole;

            var fileTarget = new FileTarget
            {
                Name = "file",
                ArchiveEvery = archive ? FileArchivePeriod.Day : FileArchivePeriod.None,
                FileName = Path.Combine(logPath, logFileName),
                ArchiveSuffixFormat = ".{1:yyyy-MM-dd}.{0:00}",
                Layout = simpleLayout,
                MaxArchiveFiles = archive ? 14 : 0,
                ArchiveAboveSize = archive ? 30 * megaByte : 0
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

            // The authorization audit trail is emitted on a dedicated category, separate from the plain-text
            // operational log, so it can be shipped to a SIEM without the two streams polluting each other.
            // Each event is already a complete ECS JSON document (built in AuthorizationAuditLog); the target
            // writes it verbatim, one object per line.
            var auditLayout = new SimpleLayout("${message}");

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
            nlogConfig.LoggingRules.Add(efCoreRule);
            nlogConfig.LoggingRules.Add(auditRule);

            if (logToConsole || AppEnvironment.RunningInContainer)
            {
                nlogConfig.LoggingRules.Add(new LoggingRule("*", logLevel, consoleTarget));
            }

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