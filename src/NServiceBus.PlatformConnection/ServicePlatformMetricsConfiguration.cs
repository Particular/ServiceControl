namespace NServiceBus
{
    using System;

    public class ServicePlatformMetricsConfiguration
    {
        public string MetricsQueue { get; set; }
        public TimeSpan Interval { get; set; }
        public string InstanceId { get; set; }
        public TimeSpan? TimeToBeReceived { get; set; }

        internal void ApplyTo(EndpointConfiguration endpointConfiguration)
        {
            if (string.IsNullOrWhiteSpace(MetricsQueue) == false)
            {
                var metrics = endpointConfiguration.EnableMetrics();
                metrics.SendMetricDataToServiceControl(MetricsQueue, Interval, InstanceId);
                if (TimeToBeReceived.HasValue)
                {
                    metrics.SetServiceControlMetricsMessageTTBR(TimeToBeReceived.Value);
                }
            }
        }
    }
}
