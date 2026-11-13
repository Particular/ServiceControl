namespace ServiceControl
{
    using Microsoft.Extensions.Logging;

    // Maps the user supplied 'RavenDBLogLevel' setting onto the value of the RavenDB server's 'Logs.MinLevel' setting.
    // HINT: RavenDB 7 removed the 'Logs.Mode' setting (and its None/Operations/Information values). The server now
    // exposes 'Logs.MinLevel', typed as Sparrow.Logging.LogLevel, which only accepts Trace, Debug, Info, Warn,
    // Error, Fatal or Off. The three verbosity tiers used before the upgrade are preserved:
    //   None -> Off, Operations (quiet/production) -> Warn, Information (verbose) -> Info
    public class RavenDbLogLevelToLogsModeMapper
    {
        public const string OffMinLevel = "Off";
        public const string WarnMinLevel = "Warn";
        public const string InfoMinLevel = "Info";

        public static string Map(string ravenDbLogLevel, ILogger logger)
        {
            switch (ravenDbLogLevel.ToLower())
            {
                case "off": // Backwards compatibility with 4.x
                case "none":
                    return OffMinLevel;
                case "trace": // Backwards compatibility with 4.x
                case "debug": // Backwards compatibility with 4.x
                case "info": // Backwards compatibility with 4.x
                case "information":
                    return InfoMinLevel;
                case "error": // Backwards compatibility with 4.x
                case "warn": // Backwards compatibility with 4.x
                case "fatal": // Backwards compatibility with 4.x
                case "operations":
                    return WarnMinLevel;
                default:
                    logger.LogWarning("Unknown log level '{RavenDbLogLevel}', mapped to '{FallbackLogLevel}'", ravenDbLogLevel, WarnMinLevel);
                    return WarnMinLevel;
            }
        }
    }
}