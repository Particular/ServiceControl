namespace ServiceControl.Operations
{
    using System;
    using NServiceBus;

    public class FailedErrorImport
    {
        public Guid Id { get; set; }
        public TransportMessage Message { get; set; }
    }
}