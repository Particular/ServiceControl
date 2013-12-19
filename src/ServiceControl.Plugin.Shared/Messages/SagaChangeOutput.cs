namespace ServiceControl.EndpointPlugin.Messages.SagaState
{
    using System;

    public class SagaChangeOutput
    {
        public string MessageType { get; set; }
        public TimeSpan? RequestedTimeout { get; set; }
        public DateTime TimeSent { get; set; }
        public DateTime? DeliveryDelay { get; set; }
        public string Destination { get; set; }
        public string ResultingMessageId { get; set; }
        public DateTime ProcessingEnded { get; set; }
        public DateTime ProcessingStarted { get; set; }
    }
}