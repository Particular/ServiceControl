namespace ServiceControl.Audit.Auditing
{
    using System;

    class FailedAuditImport
    {
        public Guid Id { get; set; }
        public FailedTransportMessage Message { get; set; }
    }
}