using System;

namespace ServiceControl.SagaAudit
{
    public class InitiatingMessage
    {
        public string MessageId { get; set; }
        public bool IsSagaTimeoutMessage { get; set; }
        public string OriginatingEndpoint { get; set; }
        public string OriginatingMachine { get; set; }
        public DateTime TimeSent { get; set; }
        public string MessageType { get; set; }
        public string Intent { get; set; }
    }
}