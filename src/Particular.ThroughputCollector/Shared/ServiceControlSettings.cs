namespace Particular.ThroughputCollector.Shared
{
    using Particular.ThroughputCollector.Contracts;

    static class ServiceControlSettings
    {
        public static readonly string MessageTransport = "ServiceControl";

        static string Queue = $"{ThroughputSettings.SettingsNamespace.Root}/{PlatformEndpointHelper.ServiceControlThroughputDataQueueSetting}";
        static string QueueDescription = $"Service Control throughput processing queue. This setting must match the {PlatformEndpointHelper.ServiceControlThroughputDataQueueSetting} setting on the Monitoring instance.";

        public static List<ThroughputConnectionSetting> GetServiceControlConnectionSettings()
        {
            return [new ThroughputConnectionSetting(Queue, QueueDescription)];
        }
    }
}
