namespace ServiceControl.Audit.Auditing
{
    using System;

    public class FailedAuditImport
    {
        public Guid Id { get; set; }
        public FailedTransportMessage Message { get; set; }
    }
}