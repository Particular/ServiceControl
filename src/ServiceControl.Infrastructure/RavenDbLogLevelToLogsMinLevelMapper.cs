namespace ServiceControl
{
    using Microsoft.Extensions.Logging;

    public class RavenDbLogLevelToLogsMinLevelMapper
    {
        public static string Map(string ravenDbLogLevel, ILogger logger)
        {
            switch (ravenDbLogLevel.ToLowerInvariant())
            {
                case "trace":
                    return "Trace";
                case "debug":
                    return "Debug";
                case "info":
                    return "Info";
                case "warn":
                    return "Warn";
                case "error":
                    return "Error";
                case "fatal":
                    return "Fatal";
                case "off":
                    return "Off";
                // Legacy Logs.Mode names, translated the way RavenDB translates them
                case "none":
                    return "Off";
                case "operations":
                    return "Info";
                case "information":
                    return "Debug";
                default:
                    logger.LogWarning("Unknown log level '{RavenDbLogLevel}', mapped to 'Warn'", ravenDbLogLevel);
                    return "Warn";
            }
        }
    }
}
