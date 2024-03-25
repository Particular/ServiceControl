namespace Particular.ThroughputCollector.Shared
{
    using Particular.ThroughputCollector.Contracts;

    static class ServiceControlSettings
    {
        public static readonly string MessageTransport = "ServiceControl";

        static string Queue = "ThroughputCollector/ServiceControl/Queue";
        static string QueueDescription = "Service Control main processing queue";

        public static List<ThroughputConnectionSetting> GetServiceControlConnectionSettings()
        {
            return [new ThroughputConnectionSetting(Queue, QueueDescription)];
        }
    }
}
