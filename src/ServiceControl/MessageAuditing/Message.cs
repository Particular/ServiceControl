namespace ServiceBus.Management.MessageAuditing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Scheduling.Messages;
    using Raven.Imports.Newtonsoft.Json;
    using ServiceControl.Contracts.Operations;
    using JsonConvert = Newtonsoft.Json.JsonConvert;

    public class Message
    {
        public Message()
        {
        }

        public Message(ErrorMessageReceived message)
        {
            Init(message.Id, message.Body, message.Headers);          
        }

        public Message(AuditMessageReceived message)
        {
            Init(message.Id, message.Body, message.Headers);
        }

        private void Init(string messageId, byte[] body, IDictionary<string, string> headers)
        {
            ReceivingEndpoint = EndpointDetails.ReceivingEndpoint(headers);

            // 3.3.x version of MessageIds had a \ in it.
            Id = string.Format("{0}-{1}", messageId.Replace(@"\", "-"), ReceivingEndpoint.Name);
            MessageId = messageId;

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

        public MessageStatus Status { get; set; }

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

        public ICollection<HistoryItem> History
        {
            get { return history ?? (history = new Collection<HistoryItem>()); }
            set { history = value; }
        }

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

        public TransportMessage IssueRetry(DateTime requestedAt)
        {
            var rawMessage = new TransportMessage(MessageId,
                Headers.Where(kv => !KeysToRemoveWhenRetryingAMessage.Contains(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value))
            {
                Body = BodyRaw,
                CorrelationId = CorrelationId,
                Recoverable = Recoverable,
                MessageIntent = MessageIntent,
                ReplyToAddress = Address.Parse(ReplyToAddress)
            };

            Status = MessageStatus.RetryIssued;

            History.Add(new HistoryItem
            {
                Action = "RetryIssued",
                Time = requestedAt
            });

            return rawMessage;
        }

        public void Update(byte[] body, IDictionary<string,string> headers )
        {
            DateTime processedAt = DateTime.MinValue;
            DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.ProcessingEnded, headers, (processEndedAt) =>
            {
                processedAt = DateTimeExtensions.ToUtcDateTime(processEndedAt);
            });
            
            if (Status == MessageStatus.Successful && ProcessedAt > processedAt)
            {
                return; //don't overwrite since this message is older
            }
            
            if (body.Length > 0 && BodyRaw.Length != body.Length)
            {
                throw new InvalidOperationException("Message bodies differ, message has been tampered with");
            }

            ProcessedAt = processedAt;

            if (Status != MessageStatus.Successful)
            {
                DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.ProcessingEnded, headers, (processingEnded) =>
                {
                    FailureDetails.ResolvedAt = DateTimeExtensions.ToUtcDateTime(processingEnded);
                });
                
                History.Add(new HistoryItem
                {
                    Action = "ErrorResolved",
                    Time = FailureDetails.ResolvedAt
                });
            }

            Status = MessageStatus.Successful;

            DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.OriginatingAddress, headers, (address) => ReplyToAddress = address);

            Statistics = GetProcessingStatistics(headers);
        }

        public void MarkAsSuccessful(IDictionary<string,string> headers)
        {
            Status = MessageStatus.Successful;
            DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.ProcessingEnded, headers, (processingEnded) =>
            {
                ProcessedAt = DateTimeExtensions.ToUtcDateTime(processingEnded);
                Statistics = GetProcessingStatistics(headers);
            });

            DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.OriginatingAddress, headers, 
                (originatingAddress) => ReplyToAddress = originatingAddress);
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

        static readonly ILog Logger = LogManager.GetLogger(typeof(Message));

        static readonly IList<string> KeysToRemoveWhenRetryingAMessage = new List<string>
        {
            NServiceBus.Headers.Retries,
            "NServiceBus.FailedQ",
            "NServiceBus.TimeOfFailure",
            "NServiceBus.ExceptionInfo.ExceptionType",
            "NServiceBus.ExceptionInfo.Message",
            "NServiceBus.ExceptionInfo.Source",
            "NServiceBus.ExceptionInfo.StackTrace"
        };

        ICollection<HistoryItem> history;

    }

    internal static class DictionaryExtensions
    {
        public static void CheckIfKeyExists(string key, IDictionary<string, string> headers, Action<string> actionToInvokeWhenKeyIsFound)
        {
            var value = string.Empty;
            if (headers.TryGetValue(key, out value))
            {
                actionToInvokeWhenKeyIsFound(value);
            }
        }
    }

    public class HistoryItem
    {
        public string Action { get; set; }

        public DateTime Time { get; set; }
    }

    public class EndpointDetails
    {
        public EndpointDetails()
        {
            
        }

        public string Name { get; set; }

        public string Machine { get; set; }

        public static EndpointDetails OriginatingEndpoint(IDictionary<string,string> headers )
        {
            var endpointDetails = new EndpointDetails();
            DictionaryExtensions.CheckIfKeyExists(Headers.OriginatingEndpoint, headers, s => endpointDetails.Name = s );
            DictionaryExtensions.CheckIfKeyExists(Headers.OriginatingMachine, headers, s => endpointDetails.Machine = s);

            if (!string.IsNullOrEmpty(endpointDetails.Name) && !string.IsNullOrEmpty(endpointDetails.Machine))
            {
                return endpointDetails;
            }

            Address address = Address.Undefined; 
            DictionaryExtensions.CheckIfKeyExists(Headers.OriginatingAddress, headers, s => address = Address.Parse(s));

            if (address != Address.Undefined)
            {
                endpointDetails.Name = address.Queue;
                endpointDetails.Machine = address.Machine;
                return endpointDetails;
            }

            return null;
        }

        public static EndpointDetails ReceivingEndpoint(IDictionary<string,string> headers)
        {
            var endpoint = new EndpointDetails();
            DictionaryExtensions.CheckIfKeyExists(Headers.ProcessingEndpoint, headers, s => endpoint.Name = s);
            DictionaryExtensions.CheckIfKeyExists(Headers.ProcessingMachine, headers, s => endpoint.Machine = s);

            if (!string.IsNullOrEmpty(endpoint.Name) && !string.IsNullOrEmpty(endpoint.Machine))
            {
                return endpoint;
            }

            var address = Address.Undefined;
            // TODO: do we need the below for the originating address!?
            DictionaryExtensions.CheckIfKeyExists(Headers.OriginatingAddress, headers, s => address = Address.Parse(s));
            //use the failed q to determine the receiving endpoint
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.FailedQ", headers, s => address = Address.Parse(s));
            
            if (string.IsNullOrEmpty(endpoint.Name))
            {
                endpoint.Name = address.Queue;
            }

            if (string.IsNullOrEmpty(endpoint.Machine))
            {
                endpoint.Machine = address.Machine;
            }

            return endpoint;
        }
    }

    public class SagaDetails
    {
        public SagaDetails()
        {
        }

        public SagaDetails(IDictionary<string, string> headers)
        {
            SagaId = headers[Headers.SagaId];
            SagaType = headers[Headers.SagaType];
            IsTimeoutMessage = headers.ContainsKey(Headers.IsSagaTimeoutMessage);
        }


        protected bool IsTimeoutMessage { get; set; }

        public string SagaId { get; set; }

        public string SagaType { get; set; }

        public static SagaDetails Parse(IDictionary<string,string> headers)
        {
            return !headers.ContainsKey(Headers.SagaId) ? null : new SagaDetails(headers);
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

        public FailureDetails(IDictionary<string,string> headers)
        {
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.FailedQ", headers, s => FailedInQueue = s);
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.TimeOfFailure", headers, s => TimeOfFailure = DateTimeExtensions.ToUtcDateTime(s));
            Exception = GetException(headers);
            NumberOfTimesFailed = 1;
        }

        public int NumberOfTimesFailed { get; set; }

        public string FailedInQueue { get; set; }

        public DateTime TimeOfFailure { get; set; }

        public ExceptionDetails Exception { get; set; }

        public DateTime ResolvedAt { get; set; }

        ExceptionDetails GetException(IDictionary<string,string> headers)
        {
            var exceptionDetails = new ExceptionDetails();
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.ExceptionInfo.ExceptionType", headers,
                s => exceptionDetails.ExceptionType = s);
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.ExceptionInfo.Message", headers,
                s => exceptionDetails.Message = s);
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.ExceptionInfo.Source", headers,
                s => exceptionDetails.Source = s);
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.ExceptionInfo.StackTrace", headers,
                           s => exceptionDetails.StackTrace = s);
            return exceptionDetails;
        }

        public void RegisterException(IDictionary<string,string> headers)
        {
            NumberOfTimesFailed++;

            var timeOfFailure = DateTime.MinValue;

            DictionaryExtensions.CheckIfKeyExists("NServiceBus.TimeOfFailure", headers, s => timeOfFailure = DateTimeExtensions.ToUtcDateTime(s));

            if (TimeOfFailure < timeOfFailure)
            {
                Exception = GetException(headers);
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