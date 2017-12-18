namespace ServiceControl.Operations
{
    using System.Collections.Generic;

    public class FailedTransportMessage
    {
        public string Id { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public byte[] Body { get; set; }
    }
}