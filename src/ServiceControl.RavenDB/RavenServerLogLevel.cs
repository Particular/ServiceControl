namespace ServiceControl.RavenDB
{
    using Microsoft.Extensions.Logging;
#if RAVENDB6
    using System;
    using System.IO;
    using Sparrow.Logging;
#endif

    public static class RavenServerLogLevel
    {
#if RAVENDB6
        public const string Default = "Operations";

        public static string Map(string ravenDbLogLevel, ILogger logger) => RavenDbLogLevelToLogsModeMapper.Map(ravenDbLogLevel, logger);

        public static string CommandLineArgument(string logLevel) => $"--Logs.Mode={logLevel}";

        public static void ConfigureEmbeddedServerLogging(string logLevel, string logPath)
        {
            var logMode = Enum.Parse<LogMode>(logLevel);

            if (logMode == LogMode.Information) // Most verbose
            {
                LoggingSource.Instance.EnableConsoleLogging();
                LoggingSource.Instance.SetupLogMode(
                    logMode,
                    Path.Combine(logPath, "Raven.Embedded"),
                    retentionTime: TimeSpan.FromDays(14),
                    retentionSize: 1024 * 1024 * 10,
                    compress: false
                );
            }
        }
#else
        public const string Default = "Warn";

        public static string Map(string ravenDbLogLevel, ILogger logger) => RavenDbLogLevelToLogsMinLevelMapper.Map(ravenDbLogLevel, logger);

        public static string CommandLineArgument(string logLevel) => $"--Logs.MinLevel={logLevel}";

        public static void ConfigureEmbeddedServerLogging(string logLevel, string logPath)
        {
        }
#endif
    }
}
