namespace ServiceControl.Contracts.MessageFailures
{
    using System;
    using NServiceBus;

    public class MessageFailed : IEvent
    {
        public string Id { get; set; }
        public DateTime FailedAt { get; set; }
        public string Endpoint { get; set; }
        public string Machine { get; set; }
        public string Reason { get; set; }
    }
}
