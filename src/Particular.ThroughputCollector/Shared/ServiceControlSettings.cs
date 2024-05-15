namespace Particular.ThroughputCollector.Shared
{
    using Particular.ThroughputCollector.Contracts;

    static class ServiceControlSettings
    {
        public static readonly string MessageTransport = "ServiceControl";

        static string SCQueue = $"{ThroughputSettings.SettingsNamespace.Root}/{PlatformEndpointHelper.ServiceControlThroughputDataQueueSetting}";
        static string SCQueueDescription = $"Service Control throughput processing queue. This setting must match the equivalent `Monitoring/{PlatformEndpointHelper.ServiceControlThroughputDataQueueSetting}` setting for the Monitoring instance.";

        static string MonitoringQueue = $"Monitoring/{PlatformEndpointHelper.ServiceControlThroughputDataQueueSetting}";
        static string MonitoringQueueDescription = $"Queue to send monitoring throughput data to for processing by ServiceControl. This setting must match the equivalent `{ThroughputSettings.SettingsNamespace.Root}/{PlatformEndpointHelper.ServiceControlThroughputDataQueueSetting}` setting for the ServiceControl instance.";

        public static List<ThroughputConnectionSetting> GetServiceControlConnectionSettings()
        {
            return [new ThroughputConnectionSetting(SCQueue, SCQueueDescription)];
        }

        public static List<ThroughputConnectionSetting> GetMonitoringConnectionSettings()
        {
            return [new ThroughputConnectionSetting(MonitoringQueue, MonitoringQueueDescription)];
        }
    }
}
