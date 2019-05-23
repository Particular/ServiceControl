namespace ServiceControl.Operations
{
    using System;

    class FailedAuditImport
    {
        public Guid Id { get; set; }
        public FailedTransportMessage Message { get; set; }
    }
}