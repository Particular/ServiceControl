namespace Particular.ThroughputCollector.Shared
{
    using Contracts;
    using Particular.ThroughputCollector.AuditThroughput;

    public static class PlatformEndpointHelper
    {
        public static bool IsPlatformEndpoint(string endpointName, ThroughputSettings throughputSettings)
        {
            return endpointName.Equals(throughputSettings.ErrorQueue, StringComparison.OrdinalIgnoreCase)
                || endpointName.Equals(throughputSettings.ServiceControlQueue, StringComparison.OrdinalIgnoreCase)
                || endpointName.EndsWith(".Timeouts", StringComparison.OrdinalIgnoreCase)
                || endpointName.EndsWith(".TimeoutsDispatcher", StringComparison.OrdinalIgnoreCase)
                || endpointName.Equals(ServiceControlSettings.ServiceControlThroughputDataQueue, StringComparison.OrdinalIgnoreCase)
                || AuditThroughputCollectorHostedService.AuditQueues.Any(a => endpointName.Equals(a, StringComparison.OrdinalIgnoreCase));
        }
    }
}
