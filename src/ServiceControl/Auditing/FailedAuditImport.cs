namespace ServiceControl.Operations
{
    using System;

    /// <summary>
    /// Legacy from the time the main instance handled also audits.
    /// </summary>
    public class FailedAuditImport
    {
        public Guid Id { get; set; }
        public FailedTransportMessage Message { get; set; }
    }
}