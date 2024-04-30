namespace Particular.ThroughputCollector.Shared
{
    using Particular.ThroughputCollector.Contracts;

    static class ServiceControlSettings
    {
        public static readonly string MessageTransport = "ServiceControl";

        static string Queue = $"{SettingsHelper.SettingsNamespace}/{PlatformEndpointHelper.ServiceControlThroughputDataQueueSetting}";
        static string QueueDescription = "Service Control throughput processing queue";

        public static List<ThroughputConnectionSetting> GetServiceControlConnectionSettings()
        {
            return [new ThroughputConnectionSetting(Queue, QueueDescription)];
        }
    }
}
