namespace ServiceControl.Operations
{
    using System.Collections.Generic;

    public class FailedTransportMessage
    {
        public required string Id { get; set; }
        public required Dictionary<string, string> Headers { get; set; }
        public required byte[] Body { get; set; }
    }
}