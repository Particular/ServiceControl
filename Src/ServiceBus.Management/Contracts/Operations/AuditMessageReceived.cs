namespace ServiceControl.Contracts.Operations
{
    using System.Collections.Generic;

    public class AuditMessageReceived
    {
        public byte[] Body { get; set; }
        public string Id { get; set; }
        public IDictionary<string, string> Headers { get; set; }
    }
}
