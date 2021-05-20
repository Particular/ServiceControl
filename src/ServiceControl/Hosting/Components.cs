namespace Particular.ServiceControl.Hosting
{
    using global::ServiceControl.CustomChecks;
    using global::ServiceControl.MessageFailures.Api;
    using global::ServiceControl.Monitoring;
    using ServiceBus.Management.Infrastructure.Settings;

    public static class Components
    {
        public static readonly ComponentInfo[] All = {
            new ComponentInfo(typeof(HeartbeatStatus).Assembly, new HeartbeatsApisModule()),
            new ComponentInfo(typeof(CustomCheck).Assembly, new CustomChecksApisModule())
        };
    }
}