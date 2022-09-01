namespace ServiceControl.Audit.Auditing
{
    public class FailedAuditImport
    {
        public string Id { get; set; }
        public FailedTransportMessage Message { get; set; }
    }
}