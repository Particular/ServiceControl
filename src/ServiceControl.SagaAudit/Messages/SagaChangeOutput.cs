namespace ServiceControl.EndpointPlugin.Messages.SagaState
{
    using System;
    using System.Text.Json.Serialization;
    using NServiceBus;
    using SagaAudit;

    public class SagaChangeOutput : ICommand
    {
        public string MessageType { get; set; }
        public DateTime TimeSent { get; set; }
        public DateTime? DeliveryAt { get; set; }
        // See the description of the converter why this is required
        [JsonConverter(typeof(CustomTimeSpanConverter))]
        public TimeSpan? DeliveryDelay { get; set; }
        public string Destination { get; set; }
        public string ResultingMessageId { get; set; }
        public string Intent { get; set; }
    }
}