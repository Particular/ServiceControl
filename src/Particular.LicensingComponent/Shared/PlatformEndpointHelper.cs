namespace Particular.LicensingComponent.Shared
{
    using Contracts;
    using Particular.LicensingComponent.AuditThroughput;

    public class PlatformEndpointHelper(ServiceControlSettings serviceControlSettings)
    {
        public bool IsPlatformEndpoint(string endpointName, ThroughputSettings throughputSettings) =>
            endpointName.Equals(throughputSettings.ErrorQueue, StringComparison.OrdinalIgnoreCase)
            || endpointName.Equals(throughputSettings.ServiceControlQueue, StringComparison.OrdinalIgnoreCase)
            || endpointName.EndsWith(".Timeouts", StringComparison.OrdinalIgnoreCase)
            || endpointName.EndsWith(".TimeoutsDispatcher", StringComparison.OrdinalIgnoreCase)
            || endpointName.StartsWith($"{throughputSettings.ServiceControlQueue}.", StringComparison.OrdinalIgnoreCase)
            || endpointName.Equals(serviceControlSettings.ServiceControlThroughputDataQueue, StringComparison.OrdinalIgnoreCase)
            || endpointName.StartsWith("Particular.Monitoring", StringComparison.OrdinalIgnoreCase)
            || AuditThroughputCollectorHostedService.AuditQueues.Any(a => endpointName.Equals(a, StringComparison.OrdinalIgnoreCase));
    }
}
