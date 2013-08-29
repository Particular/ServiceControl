namespace ServiceControl.Infrastructure.Messages
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;

    public class TransportMessageDetails : ITransportMessage
    {
            public string Id { get; set; }
            public string CorrelationId { get; set; }
            public bool Recoverable { get; set; }
            public MessageIntentEnum MessageIntent { get; set; }
            public IDictionary<string, string> Headers { get; set; }
            public bool IsControlMessage { get; set; }
            public DateTime TimeSent { get; set; }
            public byte[] Body { get; set; }
            public string ReplyToAddress { get; set; }
            public TimeSpan TimeToBeReceived { get; set; }
    }

    public interface ITransportMessage
    {
        string Id { get; set; }
        string CorrelationId { get; set; }
        bool Recoverable { get; set; }
        MessageIntentEnum MessageIntent { get; set; }
        IDictionary<string, string> Headers { get; set; }
        bool IsControlMessage { get; set; }
        DateTime TimeSent { get; set; }
        byte[] Body { get; set; }
        string ReplyToAddress { get; set; }
        TimeSpan TimeToBeReceived { get; set; }
    }
}
