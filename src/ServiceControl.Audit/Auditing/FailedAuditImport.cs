namespace ServiceControl.Audit.Auditing
{
    using System;
    using Operations;

    class FailedAuditImport
    {
        public Guid Id { get; set; }
        public FailedTransportMessage Message { get; set; }
    }
}