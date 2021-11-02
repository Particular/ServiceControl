namespace NServiceBus
{
    using System.Text.Json;

    public class ServicePlatformConnectionConfiguration
    {
        public string AuditQueue { get; set; }
        public string ErrorQueue { get; set; }
        public ServicePlatformHeartbeatConfiguration Heartbeats { get; set; }
        public ServicePlatformCustomChecksConfiguration CustomChecks { get; set; }
        public ServicePlatformSagaAuditConfiguration SagaAudit { get; set; }
        public ServicePlatformMetricsConfiguration Metrics { get; set; }

        internal void ApplyTo(EndpointConfiguration endpointConfiguration)
        {
            if (string.IsNullOrWhiteSpace(AuditQueue) == false)
            {
                endpointConfiguration.AuditProcessedMessagesTo(AuditQueue);
            }

            if (string.IsNullOrWhiteSpace(ErrorQueue) == false)
            {
                endpointConfiguration.SendFailedMessagesTo(ErrorQueue);
            }

            Heartbeats?.ApplyTo(endpointConfiguration);
            CustomChecks?.ApplyTo(endpointConfiguration);
            SagaAudit?.ApplyTo(endpointConfiguration);
            Metrics?.ApplyTo(endpointConfiguration);
        }

        public static ServicePlatformConnectionConfiguration Parse(string jsonConfiguration)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new JsonTimeSpanConverterFactory());
            return System.Text.Json.JsonSerializer.Deserialize<ServicePlatformConnectionConfiguration>(jsonConfiguration, options);
        }
    }
}