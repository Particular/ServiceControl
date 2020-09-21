namespace ServiceControl.Operations
{
    public class FailedAuditImport
    {
        public string Id { get; set; }
        public FailedTransportMessage Message { get; set; }
    }
}