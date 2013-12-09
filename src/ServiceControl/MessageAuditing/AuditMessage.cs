namespace ServiceControl.MessageAuditing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Scheduling.Messages;
    using Raven.Imports.Newtonsoft.Json;
    using Contracts.Operations;
    using JsonConvert = Newtonsoft.Json.JsonConvert;

    public class AuditMessage
    {
        public AuditMessage()
        {
        }

        public AuditMessage(AuditMessageReceived message)
        {
            Init(message.AuditMessageId,message.PhysicalMessage);
        }

        private void Init(string messageId, PhysicalMessage message)
        {
            Id = messageId;
            MessageId = message.MessageId;

            var headers = message.Headers;
            var body = message.Body;

            ReceivingEndpoint = EndpointDetails.ReceivingEndpoint(headers);


            DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.CorrelationId, headers, s => CorrelationId = s);
            //TODO: Do we need to expose Recoverable in AuditMessageReceived? I don't see this in the headers
            MessageIntentEnum messageIntent;
            Enum.TryParse(headers[NServiceBus.Headers.MessageIntent], true, out messageIntent);
            MessageIntent = messageIntent;
            Headers = headers.Select(header => new KeyValuePair<string, string>(header.Key, header.Value));

            DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.TimeSent, headers, (timeSentValue) =>
            {
                TimeSent = DateTimeExtensions.ToUtcDateTime(timeSentValue);
            });

            DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.ControlMessageHeader, headers,
                (controlMessage) =>
                {
                    MessageType = "SystemMessage";
                    IsSystemMessage = true;
                });

            if (!IsSystemMessage)
            {
                DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.EnclosedMessageTypes, headers,
                (messageType) =>
                {
                    MessageType = GetMessageType(messageType);
                    IsSystemMessage = DetectSystemMessage(messageType);
                });

                ContentType = DetermineContentType(headers);
                Body = DeserializeBody(body, ContentType);
                BodyRaw = body;

                DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.RelatedTo, headers, msgId => RelatedToMessageId = msgId);
                DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.CorrelationId, headers, id => CorrelationId = id);
                DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.ConversationId, headers,
                    convId => ConversationId = convId);
                OriginatingSaga = SagaDetails.Parse(headers);
                DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.IsDeferredMessage, headers, isDeferred => IsDeferredMessage = true);
            }

            
            OriginatingEndpoint = EndpointDetails.OriginatingEndpoint(headers);
   
        }

        [JsonIgnore]
        public string Url { get; set; }

        [JsonIgnore]
        public string RetryUrl { get; set; }

        [JsonIgnore]
        public string ConversationUrl { get; set; }


        public bool IsDeferredMessage { get; set; }

        public string Id { get; set; }

        public string MessageId { get; set; }

        public string MessageType { get; set; }

        public IEnumerable<KeyValuePair<string, string>> Headers { get; set; }

        public string Body { get; set; }

        public byte[] BodyRaw { get; set; }

        public string RelatedToMessageId { get; set; }

        public string CorrelationId { get; set; }

        public string ConversationId { get; set; }

        public EndpointDetails OriginatingEndpoint { get; set; }

        public EndpointDetails ReceivingEndpoint { get; set; }

        public SagaDetails OriginatingSaga { get; set; }

        public FailureDetails FailureDetails { get; set; }

        public DateTime TimeSent { get; set; }

        public MessageStatistics Statistics { get; set; }

        public string ReplyToAddress { get; set; }

        public DateTime ProcessedAt { get; set; }

        public string ContentType { get; set; }

        public bool Recoverable { get; set; }

        public MessageIntentEnum MessageIntent { get; set; }


        public bool IsSystemMessage { get; set; }

        bool DetectSystemMessage(string messageTypeString)
        {
            return messageTypeString.Contains(typeof(ScheduledTask).FullName);
        }

        string GetMessageType(string messageTypeString)
        {
            if (!messageTypeString.Contains(","))
            {
                return messageTypeString;
            }

            return messageTypeString.Split(',').First();
        }

        string DetermineContentType(IDictionary<string,string> headers)
        {
            var contentType = "application/xml"; //default to xml for now
            DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.ContentType, headers, s => contentType = s);
            return contentType;
        }

        static string DeserializeBody(byte[] body, string contentType)
        {
            var bodyString = Encoding.UTF8.GetString(body);

            if (contentType == "application/json" || contentType == "text/json")
            {
                return bodyString;
            }

            if (contentType != "text/xml" && contentType != "application/xml")
            {
                return null;
            }

            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(bodyString);
                return JsonConvert.SerializeXmlNode(doc.DocumentElement);
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to convert XML payload to json", ex);
                return null;
            }
        }

       

      
        MessageStatistics GetProcessingStatistics(IDictionary<string,string> headers)
        {
            var messageStatistics = new MessageStatistics();
            var processingEnded = DateTime.MinValue;
            var timeSent = DateTime.MinValue;
            var processingStarted = DateTime.MinValue;

            DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.ProcessingEnded, headers, (ended) =>
            {
                processingEnded = DateTimeExtensions.ToUtcDateTime(ended);
            });
            DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.TimeSent, headers, (time) =>
            {
                timeSent = DateTimeExtensions.ToUtcDateTime(time);
            });
            DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.ProcessingStarted, headers, (started) =>
            {
                processingStarted = DateTimeExtensions.ToUtcDateTime(started);
            });

            if (processingEnded != DateTime.MinValue && timeSent != DateTime.MinValue)
            {
                messageStatistics.CriticalTime = processingEnded - timeSent;
            }
            if (processingEnded != DateTime.MinValue && processingStarted != DateTime.MinValue)
            {
                messageStatistics.ProcessingTime = processingEnded - processingStarted;
            }

            return messageStatistics;

        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(AuditMessage));

       
    }
}