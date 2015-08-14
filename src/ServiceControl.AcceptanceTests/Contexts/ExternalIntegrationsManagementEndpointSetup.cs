namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System.IO;
    using NLog;
    using NLog.Config;
    using NLog.Targets;
    using NServiceBus.AcceptanceTesting.Support;

    public class ExternalIntegrationsManagementEndpointSetup : ManagementEndpointSetup
    {
        protected override LoggingConfiguration SetupLogging(EndpointConfiguration endpointConfiguration)
        {
            var logDir = ".\\logfiles\\";

            Directory.CreateDirectory(logDir);

            var logFile = Path.Combine(logDir, endpointConfiguration.EndpointName + ".txt");

            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }

            var logLevel = "ERROR";

            var nlogConfig = new LoggingConfiguration();

            var fileTarget = new FileTarget
            {
                FileName = logFile,
            };

            nlogConfig.LoggingRules.Add(new LoggingRule("Raven.*", LogLevel.Warn, fileTarget) { Final = true });
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.FromString(logLevel), fileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("ServiceControl.ExternalIntegrations.*", LogLevel.Debug, fileTarget));
            nlogConfig.AddTarget("debugger", fileTarget);
            return nlogConfig;
        }
    }
}