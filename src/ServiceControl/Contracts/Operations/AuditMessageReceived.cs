namespace ServiceControl.Contracts.Operations
{
    public class AuditMessageReceived
    {
        public PhysicalMessage PhysicalMessage { get; set; }
        public string AuditMessageId { get; set; }
    }
}
