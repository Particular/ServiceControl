namespace ServiceControl.SagaAudit
{
    using System;

    public class ResultingMessage
    {
        public DateTime? DeliveryDelay { get; set; }
        public string Destination { get; set; }
        public TimeSpan? RequestedTimeout { get; set; }
        public string ResultingMessageId { get; set; }
        public string TimeSent { get; set; }
        public string MessageType { get; set; }
    }
}