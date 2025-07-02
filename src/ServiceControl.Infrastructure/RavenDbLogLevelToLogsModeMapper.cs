namespace ServiceControl
{
    using Microsoft.Extensions.Logging;

    public class RavenDbLogLevelToLogsModeMapper
    {
        public static string Map(string ravenDbLogLevel, ILogger logger)
        {
            switch (ravenDbLogLevel.ToLower())
            {
                case "off": // Backwards compatibility with 4.x
                case "none":
                    return "None";
                case "trace": // Backwards compatibility with 4.x
                case "debug": // Backwards compatibility with 4.x
                case "info": // Backwards compatibility with 4.x
                case "information":
                    return "Information";
                case "error": // Backwards compatibility with 4.x
                case "warn": // Backwards compatibility with 4.x
                case "fatal": // Backwards compatibility with 4.x
                case "operations":
                    return "Operations";
                default:
                    logger.LogWarning("Unknown log level '{RavenDbLogLevel}', mapped to 'Operations'", ravenDbLogLevel);
                    return "Operations";
            }
        }
    }
}