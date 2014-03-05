namespace ServiceControl.Plugin.SagaAudit.Sample
{
    using NLog;
    using NLog.Config;
    using NLog.Targets;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Logging.Loggers.NLogAdapter;

    public class EndpointConfig: IConfigureThisEndpoint, AsA_Publisher, IWantCustomInitialization
    {
        public void Init()
        {
            Configure.Features.Enable<Audit>();
            Configure.Features.Disable<SecondLevelRetries>();
            ConfigureLogging();
            Configure.Serialization.Json();
            Configure.With().DefaultBuilder();
        }

        static void ConfigureLogging()
        {
            var nlogConfig = new LoggingConfiguration();

            var consoleTarget = new ColoredConsoleTarget
                {
                    UseDefaultRowHighlightingRules = true,
                    Layout = "${level:uppercase=true} | ${logger} | ${threadid} | ${message}"
                };

            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, consoleTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("NServiceBus.Pipeline.*", LogLevel.Debug, consoleTarget));
            nlogConfig.AddTarget("console", consoleTarget);
            NLogConfigurator.Configure(new object[] {consoleTarget}, "Info");
            LogManager.Configuration = nlogConfig;
        }
    }
}
