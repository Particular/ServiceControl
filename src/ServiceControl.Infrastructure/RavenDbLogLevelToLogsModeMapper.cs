namespace ServiceControl
{
    using Microsoft.Extensions.Logging;
    using NServiceBus.Logging;

    public class RavenDbLogLevelToLogsModeMapper
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenDbLogLevelToLogsModeMapper));

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
                    Logger.WarnFormat("Unknown log level '{0}', mapped to 'Operations'", ravenDbLogLevel);
                    return "Operations";
            }
        }
    }
}