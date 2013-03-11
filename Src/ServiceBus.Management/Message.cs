namespace ServiceBus.Management
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using NServiceBus;
    using NServiceBus.Unicast.Transport;
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
            Headers = message.Headers.Select(header => new KeyValuePair<string, string>(header.Key, header.Value));
            TimeSent = DateTimeExtensions.ToUtcDateTime(message.Headers[NServiceBus.Headers.TimeSent]);

            if (message.IsControlMessage())
            {
                MessageType = "SystemMessage";
            }
            else
            {
                MessageType = message.Headers[NServiceBus.Headers.EnclosedMessageTypes];
                Body = DeserializeBody(message);
                BodyRaw = message.Body;
                RelatedToMessageId = message.Headers.ContainsKey(NServiceBus.Headers.RelatedTo) ? message.Headers[NServiceBus.Headers.RelatedTo] : null;
                ConversationId = message.Headers[NServiceBus.Headers.ConversationId];
                OriginatingSaga = SagaDetails.Parse(message);
                IsDeferredMessage = message.Headers.ContainsKey(NServiceBus.Headers.IsDeferredMessage);
            }

            OriginatingEndpoint = EndpointDetails.OriginatingEndpoint(message);
            ReceivingEndpoint = EndpointDetails.ReceivingEndpoint(message);
        }


        public bool IsDeferredMessage { get; set; }

        public string Id { get; set; }

        public string MessageType { get; set; }

        public IEnumerable<KeyValuePair<string, string>> Headers { get; set; }

        public string Body { get; set; }

        public byte[] BodyRaw { get; set; }

        public string RelatedToMessageId { get; set; }

        public string CorrelationId { get; set; }

        public string ConversationId { get; set; }

        public MessageStatus Status { get; set; }

        public EndpointDetails OriginatingEndpoint { get; set; }

        public EndpointDetails ReceivingEndpoint { get; set; }

        public SagaDetails OriginatingSaga { get; set; }

        public FailureDetails FailureDetails { get; set; }

        public DateTime TimeSent { get; set; }

        public MessageStatistics Statistics { get; set; }

        public string ReplyToAddress { get; set; }

        public DateTime ProcessedAt { get; set; }

        static string DeserializeBody(TransportMessage message)
        {
            //todo examine content type
            var doc = new XmlDocument();
            doc.LoadXml(Encoding.UTF8.GetString(message.Body));
            return JsonConvert.SerializeXmlNode(doc.DocumentElement);
        }
    }

    public class EndpointDetails
    {
        public string Name { get; set; }

        public string Machine { get; set; }

        public static EndpointDetails OriginatingEndpoint(TransportMessage message)
        {
            if (message.Headers.ContainsKey(Headers.OriginatingEndpoint))
            {
                return new EndpointDetails
                {

                    Name = message.Headers[Headers.OriginatingEndpoint],
                    Machine = message.Headers[Headers.OriginatingMachine]
                };

            }

            if (message.Headers.ContainsKey(Headers.OriginatingAddress))
            {

                var address = Address.Parse(message.Headers[Headers.OriginatingAddress]);

                return new EndpointDetails
                    {

                        Name = address.Queue,
                        Machine = address.Machine
                    };
            }
                

            return null;
        }


        public static EndpointDetails ReceivingEndpoint(TransportMessage message)
        {
            var endpoint = new EndpointDetails();

            //use the failed q to determine the receiving endpoint
            if (message.Headers.ContainsKey("NServiceBus.FailedQ"))
            {
                var failedAddress = Address.Parse(message.Headers["NServiceBus.FailedQ"]);


                endpoint.Name = failedAddress.Queue;
                endpoint.Machine = failedAddress.Machine;
            }
            else
            {
                //for successfull messages the replytoaddress will be the address of the endpoint who processed the message
                endpoint.Name = message.ReplyToAddress.Queue;
                endpoint.Machine = message.ReplyToAddress.Machine;
            }

            return endpoint;
        }
    }

    public class SagaDetails
    {
        public SagaDetails()
        {
        }

        public SagaDetails(TransportMessage message)
        {
            SagaId = message.Headers[Headers.SagaId];
            SagaType = message.Headers[Headers.SagaType];
            IsTimeoutMessage = message.Headers.ContainsKey(Headers.IsSagaTimeoutMessage);
        }


        protected bool IsTimeoutMessage { get; set; }

        public string SagaId { get; set; }

        public string SagaType { get; set; }

        public static SagaDetails Parse(TransportMessage message)
        {
            return !message.Headers.ContainsKey(Headers.SagaId) ? null : new SagaDetails(message);
        }
    }

    public class MessageStatistics
    {
        public TimeSpan CriticalTime { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    public class FailureDetails
    {
        public FailureDetails()
        {
        }

        public FailureDetails(TransportMessage message)
        {
            FailedInQueue = message.Headers["NServiceBus.FailedQ"];
            TimeOfFailure = DateTimeExtensions.ToUtcDateTime(message.Headers["NServiceBus.TimeOfFailure"]);
            Exception = GetException(message);
            NumberOfTimesFailed = 1;
        }

        public int NumberOfTimesFailed { get; set; }

        public string FailedInQueue { get; set; }

        public DateTime TimeOfFailure { get; set; }

        public ExceptionDetails Exception { get; set; }

        public DateTime ResolvedAt { get; set; }

        ExceptionDetails GetException(TransportMessage message)
        {
            return new ExceptionDetails
            {
                ExceptionType = message.Headers["NServiceBus.ExceptionInfo.ExceptionType"],
                Message = message.Headers["NServiceBus.ExceptionInfo.Message"],
                Source = message.Headers["NServiceBus.ExceptionInfo.Source"],
                StackTrace = message.Headers["NServiceBus.ExceptionInfo.StackTrace"]
            };
        }

        public void RegisterException(TransportMessage message)
        {
            NumberOfTimesFailed++;

            var timeOfFailure = DateTimeExtensions.ToUtcDateTime(message.Headers["NServiceBus.TimeOfFailure"]);

            if (TimeOfFailure < timeOfFailure)
            {
                Exception = GetException(message);
                TimeOfFailure = timeOfFailure;
            }

            //todo -  add history
        }
    }

    public class ExceptionDetails
    {
        public string ExceptionType { get; set; }

        public string Message { get; set; }

        public string Source { get; set; }

        public string StackTrace { get; set; }
    }
}