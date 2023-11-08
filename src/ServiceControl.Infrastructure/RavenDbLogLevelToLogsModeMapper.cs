namespace ServiceControl
{
    using NServiceBus.Logging;

    public class RavenDbLogLevelToLogsModeMapper
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenDbLogLevelToLogsModeMapper));

        public static string Map(string ravenDbLogLevel)
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
                case "operations":
                    return "Operations";
                default:
                    Logger.WarnFormat("Unknown log level '{0}', mapped to 'Operations'");
                    return "Operations";
            }
        }
    }
}