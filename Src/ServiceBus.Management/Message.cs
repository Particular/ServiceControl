namespace ServiceBus.Management
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using NServiceBus;
    using Newtonsoft.Json;

    public class Message
    {
        public Message()
        {
        }

        public Message(TransportMessage message)
        {
            Id = message.IdForCorrelation;
            CorrelationId = message.CorrelationId;
            MessageType = message.Headers[NServiceBus.Headers.EnclosedMessageTypes];
            Headers = message.Headers.Select(header => new KeyValuePair<string, string>(header.Key, header.Value));
            TimeSent = DateTimeExtensions.ToUtcDateTime(message.Headers[NServiceBus.Headers.TimeSent]);
            Body = DeserializeBody(message);
            BodyRaw = message.Body;
            RelatedToMessageId = message.Headers.ContainsKey(NServiceBus.Headers.RelatedTo) ?  message.Headers[NServiceBus.Headers.RelatedTo] : null;
            ConversationId = message.Headers[NServiceBus.Headers.ConversationId];
            Status = MessageStatus.Failed;
            Endpoint = GetEndpoint(message);
        }

        string GetEndpoint(TransportMessage message)
        {
            if (message.Headers.ContainsKey(NServiceBus.Headers.OriginatingEndpoint))
                return message.Headers[NServiceBus.Headers.OriginatingEndpoint];

            if (message.ReplyToAddress != null)
                return message.ReplyToAddress.ToString();

            return null;
        }

        static string DeserializeBody(TransportMessage message)
        {
            //todo examine content type
            var doc = new XmlDocument();
            doc.LoadXml(Encoding.UTF8.GetString(message.Body));
            return JsonConvert.SerializeXmlNode(doc.DocumentElement);
        }


        public string Id { get; set; }

        public string MessageType { get; set; }

        public IEnumerable<KeyValuePair<string, string>> Headers { get; set; }

        public string Body { get; set; }

        public byte[] BodyRaw { get; set; }

        public string RelatedToMessageId { get; set; }

        public string CorrelationId { get; set; }

        public string ConversationId { get; set; }

        public MessageStatus Status { get; set; }

        public string Endpoint { get; set; }

        public FailureDetails FailureDetails { get; set; }

        public DateTime TimeSent { get; set; }

        public MessageStatistics Statistics { get; set; }
    }

    public class MessageStatistics
    {
        public TimeSpan CriticalTime{ get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    public class FailureDetails
    {
        public int NumberOfTimesFailed { get; set; }

        public string FailedInQueue { get; set; }

        public DateTime TimeOfFailure { get; set; }

        public ExceptionDetails Exception { get; set; }

        public DateTime ResolvedAt { get; set; }
    }

    public class ExceptionDetails
    {
        public string ExceptionType { get; set; }

        public string Message { get; set; }

        public string Source { get; set; }

        public string StackTrace { get; set; }
    }
}