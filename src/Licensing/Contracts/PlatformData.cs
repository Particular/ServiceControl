namespace Particular.License.Contracts
{
    class PlatformData
    {
        public required string ServiceControlAPI { get; set; }
        public required string Broker { get; set; }
        public required string ErrorQueue { get; set; }
        public required string AuditQueue { get; set; } //NOTE can we get this?
        public required string TransportConnectionString { get; set; }
        public required string PersistenceType { get; set; }
    }
}
