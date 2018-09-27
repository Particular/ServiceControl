namespace ServiceControl.Operations
{
    using System;

    class FailedErrorImport
    {
        public Guid Id { get; set; }
        public FailedTransportMessage Message { get; set; }
    }
}