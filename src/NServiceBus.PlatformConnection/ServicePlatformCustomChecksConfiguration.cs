namespace NServiceBus
{
    using System;

    public class ServicePlatformCustomChecksConfiguration
    {
        public string CustomCheckQueue { get; set; }
        public TimeSpan? TimeToLive { get; set; }

        internal void ApplyTo(EndpointConfiguration endpointConfiguration)
        {
            if (string.IsNullOrWhiteSpace(CustomCheckQueue) == false)
            {
                endpointConfiguration.ReportCustomChecksTo(CustomCheckQueue, TimeToLive);
            }
        }
    }
}