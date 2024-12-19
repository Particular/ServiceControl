namespace Particular.LicensingComponent.Shared
{
    using Particular.LicensingComponent.Contracts;
    using ServiceControl.Configuration;

    public static class ServiceControlSettings
    {
        public static readonly string MessageTransport = "ServiceControl";

        public static string ServiceControlThroughputDataQueueSetting = "ServiceControlThroughputDataQueue";
        public static string ServiceControlThroughputDataQueue = SettingsReader.Read(ThroughputSettings.SettingsNamespace, ServiceControlThroughputDataQueueSetting, "ServiceControl.ThroughputData");

        static string SCQueue = $"{ThroughputSettings.SettingsNamespace.Root}/{ServiceControlThroughputDataQueueSetting}";
        static string SCQueueDescription = $"Service Control throughput processing queue. This setting must match the equivalent `Monitoring/{ServiceControlThroughputDataQueueSetting}` setting for the Monitoring instance.";

        static string MonitoringQueue = $"Monitoring/{ServiceControlThroughputDataQueueSetting}";
        static string MonitoringQueueDescription = $"Queue to send monitoring throughput data to for processing by ServiceControl. This setting must match the equivalent `{ThroughputSettings.SettingsNamespace.Root}/{ServiceControlThroughputDataQueueSetting}` setting for the ServiceControl instance.";

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