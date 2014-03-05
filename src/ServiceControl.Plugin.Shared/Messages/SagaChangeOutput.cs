namespace ServiceControl.EndpointPlugin.Messages.SagaState
{
    using System;

    public class SagaChangeOutput
    {
        public string MessageType { get; set; }
        public DateTime TimeSent { get; set; }
        public DateTime? DeliveryAt { get; set; }
        public TimeSpan? DeliveryDelay { get; set; }
        public string Destination { get; set; }
        public string ResultingMessageId { get; set; }
        public string Intent { get; set; }
    }
}