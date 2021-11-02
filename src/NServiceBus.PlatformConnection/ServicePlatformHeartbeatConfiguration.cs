namespace NServiceBus
{
    using System;

    public class ServicePlatformHeartbeatConfiguration
    {
        public string HeartbeatQueue { get; set; }
        public TimeSpan? Frequency { get; set; }
        public TimeSpan? TimeToLive { get; set; }

        internal void ApplyTo(EndpointConfiguration endpointConfiguration)
        {
            if (string.IsNullOrWhiteSpace(HeartbeatQueue) == false)
            {
                endpointConfiguration.SendHeartbeatTo(HeartbeatQueue, Frequency, TimeToLive);
            }
        }
    }
}