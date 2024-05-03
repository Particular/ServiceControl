namespace Particular.ThroughputCollector.Shared
{
    using Contracts;
    using ServiceControl.Configuration;

    public static class PlatformEndpointHelper
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

        public static string ServiceControlThroughputDataQueue = SettingsReader.Read(ThroughputSettings.SettingsNamespace, ServiceControlThroughputDataQueueSetting, "ServiceControl.ThroughputData");
        public static string ServiceControlThroughputDataQueueSetting = "ServiceControlThroughputDataQueue";
    }
}
