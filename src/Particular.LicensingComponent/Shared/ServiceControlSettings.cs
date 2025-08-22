namespace Particular.LicensingComponent.Shared
{
    using Particular.LicensingComponent.Contracts;
    using ServiceControl.Configuration;

    public static class ServiceControlSettings
    {
        public static readonly string MessageTransport = "ServiceControl";

        public static string ServiceControlThroughputDataQueueSetting = "ServiceControlThroughputDataQueue";
        public static string ServiceControlThroughputDataQueue = SettingsReader.Read(ThroughputSettings.SettingsNamespace, ServiceControlThroughputDataQueueSetting, "ServiceControl.ThroughputData");

        static string MonitoringQueue = $"Monitoring/{ServiceControlThroughputDataQueueSetting}";
        static string MonitoringQueueDescription = $"Queue to send monitoring throughput data to for processing by ServiceControl. This setting only needs to be specified if the Monitoring instance is not hosted in the same machine as the Error instance is running on.";

        public static List<ThroughputConnectionSetting> GetServiceControlConnectionSettings()
        {
            return [];
        }

        public static List<ThroughputConnectionSetting> GetMonitoringConnectionSettings()
        {
            return [new ThroughputConnectionSetting(MonitoringQueue, MonitoringQueueDescription)];
        }
    }
}
