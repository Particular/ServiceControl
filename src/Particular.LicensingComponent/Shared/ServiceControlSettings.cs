namespace Particular.LicensingComponent.Shared
{
    using Microsoft.Extensions.Configuration;
    using Particular.LicensingComponent.Contracts;

    public class ServiceControlSettings       (IConfiguration configuration)
    {
        public const string MessageTransport = "ServiceControl";
        public const string ServiceControlThroughputDataQueueSetting = "ServiceControlThroughputDataQueue";
        public string ServiceControlThroughputDataQueue => configuration
            .GetSection(ThroughputSettings.SettingsNamespace.Root)
            .GetValue<string>(ServiceControlThroughputDataQueueSetting, "ServiceControl.ThroughputData");

        static string MonitoringQueue = $"Monitoring/{ServiceControlThroughputDataQueueSetting}";
        static string MonitoringQueueDescription = "Queue to send monitoring throughput data to for processing by ServiceControl. This setting only needs to be specified if the Monitoring instance is not hosted in the same machine as the Error instance is running on.";

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