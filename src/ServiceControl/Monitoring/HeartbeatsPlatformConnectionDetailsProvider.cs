namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using Connection;
    using NServiceBus;
    using NServiceBus.Settings;
    using ServiceBus.Management.Infrastructure.Settings;

    class HeartbeatsPlatformConnectionDetailsProvider : IProvidePlatformConnectionDetails
    {
        readonly Settings settings;
        readonly string instanceMainQueue;

        public HeartbeatsPlatformConnectionDetailsProvider(Settings settings, ReadOnlySettings endpointSettings)
        {
            this.settings = settings;
            instanceMainQueue = endpointSettings.LocalAddress();
        }

        public Task ProvideConnectionDetails(PlatformConnectionDetails connection)
        {
            // NOTE: The default grace period is 40s and the default frequency is 10s.
            // In a low-latency environment, an endpoint would need to miss more than 4 heartbeats to be considered down
            var frequency = TimeSpan.FromTicks(settings.HeartbeatGracePeriod.Ticks / 4);
            var timeToLive = TimeSpan.FromTicks(frequency.Ticks * 4);
            connection.Add(
                "Heartbeats",
                new HeartbeatsConnectionDetails
                {
                    Enabled = true,
                    HeartbeatsQueue = instanceMainQueue,
                    Frequency = frequency,
                    TimeToLive = timeToLive
                });

            return Task.CompletedTask;
        }

        // HINT: This should match the type in the PlatformConnector package
        class HeartbeatsConnectionDetails
        {
            public bool Enabled { get; set; }
            public string HeartbeatsQueue { get; set; }
            public TimeSpan Frequency { get; set; }
            public TimeSpan TimeToLive { get; set; }
        }
    }
}