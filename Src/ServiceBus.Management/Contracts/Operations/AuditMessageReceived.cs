namespace ServiceControl.Contracts.Operations
{
    using System.Collections.Generic;
    using NServiceBus;

    public class AuditMessageReceived : IEvent
    {
        public byte[] Body { get; set; }
        public string Id { get; set; }
        public IDictionary<string, string> Headers { get; set; }
    }
}
