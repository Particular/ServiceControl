namespace Particular.ThroughputCollector.Contracts
{
    using System.Net;

    class ServiceControlEndpoint
    {
        public string Name { get; set; } = "";
        public string UrlName => WebUtility.UrlEncode(Name);
        public bool HeartbeatsEnabled { get; set; }
        public bool ReceivingHeartbeats { get; set; }
        public bool CheckHourlyAuditDataIfNoMonitoringData { get; set; }
        public AuditCount[] AuditCounts { get; set; } = Array.Empty<AuditCount>();
    }
}
