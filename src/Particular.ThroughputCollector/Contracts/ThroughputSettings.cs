namespace Particular.ThroughputCollector.Contracts
{
    public class ThroughputSettings
    {
        public required string ServiceControlAPI { get; set; }
        public Broker Broker { get; set; }
        public required string ErrorQueue { get; set; }
        public required string AuditQueue { get; set; } //NOTE can we get this?
        public required string TransportConnectionString { get; set; }
        public required string PersistenceType { get; set; }
    }
}
