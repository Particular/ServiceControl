namespace ServiceControl.Operations
{
    using System;

    public class FailedErrorImport
    {
        public Guid Id { get; set; }
        public FailedTransportMessage Message { get; set; }
    }
}