namespace ServiceControl.Operations
{
    using System;

    public class FailedAuditImport
    {
        public Guid Id { get; set; }
        public FailedTransportMessage Message { get; set; }
    }
}