namespace Particular.ThroughputCollector.Shared
{
    using Particular.ThroughputCollector.Contracts;
    using ServiceControl.Configuration;

    static class PlatformEndpointHelper
    {
        public static bool IsPlatformEndpoint(string endpointName, ThroughputSettings throughputSettings)
        {
            return endpointName.Equals(throughputSettings.ErrorQueue, StringComparison.OrdinalIgnoreCase)
                || endpointName.Equals(throughputSettings.ServiceControlQueue, StringComparison.OrdinalIgnoreCase)
                || endpointName.EndsWith(".Timeouts", StringComparison.OrdinalIgnoreCase)
                || endpointName.EndsWith(".TimeoutsDispatcher", StringComparison.OrdinalIgnoreCase)
                || endpointName.Equals(ServiceControlThroughputDataQueue, StringComparison.OrdinalIgnoreCase)
                || AuditQueues.Any(a => endpointName.Equals(a, StringComparison.OrdinalIgnoreCase));
        }

        public static List<string> AuditQueues { get; set; } = [];

        public static string ServiceControlThroughputDataQueue = SettingsReader.Read(new SettingsRootNamespace(SettingsNamespace), "ServiceControlThroughputDataQueue", "ServiceControl.ThroughputData");

        public static readonly string SettingsNamespace = "ThroughputCollector";
    }
}
