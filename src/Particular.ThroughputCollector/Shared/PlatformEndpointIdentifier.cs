namespace Particular.ThroughputCollector.Shared
{
    using Particular.ThroughputCollector.Contracts;

    static class PlatformEndpointIdentifier
    {
        public static bool IsPlatformEndpoint(string endpointName, ThroughputSettings throughputSettings)
        {
            return endpointName.Equals(throughputSettings.ErrorQueue, StringComparison.OrdinalIgnoreCase)
                || endpointName.Equals(throughputSettings.ServiceControlQueue, StringComparison.OrdinalIgnoreCase)
                || endpointName.EndsWith(".Timeouts", StringComparison.OrdinalIgnoreCase)
                || endpointName.EndsWith(".TimeoutsDispatcher", StringComparison.OrdinalIgnoreCase)
                || AuditQueues.Any(a => endpointName.Equals(a, StringComparison.OrdinalIgnoreCase));
        }

        public static List<string> AuditQueues { get; set; } = [];
    }
}
