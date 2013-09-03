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
            Id = messageId + "-" + ReceivingEndpoint.Name;
            MessageId = messageId;
            CorrelationId = headers[NServiceBus.Headers.CorrelationId];
            //TODO: Do we need to expose Recoverable in AuditMessageReceived? I don't see this in the headers
            MessageIntentEnum messageIntent;
            Enum.TryParse(headers[NServiceBus.Headers.MessageIntent], true, out messageIntent);
            MessageIntent = messageIntent;
            Headers = headers.Select(header => new KeyValuePair<string, string>(header.Key, header.Value));
            TimeSent = DateTimeExtensions.ToUtcDateTime(headers[NServiceBus.Headers.TimeSent]);
            if (headers.ContainsKey(NServiceBus.Headers.ControlMessageHeader))
            {
                MessageType = "SystemMessage";
                IsSystemMessage = true;
            }
            else
            {
                var messageTypeString = headers[NServiceBus.Headers.EnclosedMessageTypes];

                MessageType = GetMessageType(messageTypeString);
                IsSystemMessage = DetectSystemMessage(messageTypeString);
                ContentType = DetermineContentType(headers);
                Body = DeserializeBody(body, ContentType);
                BodyRaw = body;
                RelatedToMessageId = headers.ContainsKey(NServiceBus.Headers.RelatedTo)
                    ? headers[NServiceBus.Headers.RelatedTo]
                    : null;
                ConversationId = headers[NServiceBus.Headers.ConversationId];
                OriginatingSaga = SagaDetails.Parse(headers);
                IsDeferredMessage = headers.ContainsKey(NServiceBus.Headers.IsDeferredMessage);
                
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
            if (headers.ContainsKey(NServiceBus.Headers.ContentType))
            {
                return headers[NServiceBus.Headers.ContentType];
            }

            return "application/xml"; //default to xml for now
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
            var processedAt = DateTimeExtensions.ToUtcDateTime(headers[NServiceBus.Headers.ProcessingEnded]);

            if (Status == MessageStatus.Successful && ProcessedAt > processedAt)
            {
                return; //don't overwrite since this message is older
            }

            if (BodyRaw.Length != body.Length)
            {
                throw new InvalidOperationException("Message bodies differ, message has been tampered with");
            }


            ProcessedAt = processedAt;

            if (Status != MessageStatus.Successful)
            {
                FailureDetails.ResolvedAt =
                    DateTimeExtensions.ToUtcDateTime(headers[NServiceBus.Headers.ProcessingEnded]);
                History.Add(new HistoryItem
                {
                    Action = "ErrorResolved",
                    Time = FailureDetails.ResolvedAt
                });
            }

            Status = MessageStatus.Successful;

            if (headers.ContainsKey(NServiceBus.Headers.OriginatingAddress))
            {
                ReplyToAddress = headers[NServiceBus.Headers.OriginatingAddress];
            }

            Statistics = GetProcessingStatistics(headers);
        }

        public void MarkAsSuccessful(IDictionary<string,string> headers)
        {
            Status = MessageStatus.Successful;
            ProcessedAt = DateTimeExtensions.ToUtcDateTime(headers[NServiceBus.Headers.ProcessingEnded]);
            Statistics = GetProcessingStatistics(headers);

            if (headers.ContainsKey(NServiceBus.Headers.OriginatingAddress))
            {
                ReplyToAddress = headers[NServiceBus.Headers.OriginatingAddress];
            }
        }

        MessageStatistics GetProcessingStatistics(IDictionary<string,string> headers)
        {
            return new MessageStatistics
            {
                CriticalTime =
                    DateTimeExtensions.ToUtcDateTime(headers[NServiceBus.Headers.ProcessingEnded]) -
                    DateTimeExtensions.ToUtcDateTime(headers[NServiceBus.Headers.TimeSent]),
                ProcessingTime =
                    DateTimeExtensions.ToUtcDateTime(headers[NServiceBus.Headers.ProcessingEnded]) -
                    DateTimeExtensions.ToUtcDateTime(headers[NServiceBus.Headers.ProcessingStarted])
            };
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

    public class HistoryItem
    {
        public string Action { get; set; }

        public DateTime Time { get; set; }
    }

    public class EndpointDetails
    {
        public string Name { get; set; }

        public string Machine { get; set; }

        public static EndpointDetails OriginatingEndpoint(IDictionary<string,string> headers )
        {
            if (headers.ContainsKey(Headers.OriginatingEndpoint))
            {
                return new EndpointDetails
                {
                    Name = headers[Headers.OriginatingEndpoint],
                    Machine = headers[Headers.OriginatingMachine]
                };
            }

            if (headers.ContainsKey(Headers.OriginatingAddress))
            {
                var address = Address.Parse(headers[Headers.OriginatingAddress]);

                return new EndpointDetails
                {
                    Name = address.Queue,
                    Machine = address.Machine
                };
            }


            return null;
        }


        public static EndpointDetails ReceivingEndpoint(IDictionary<string,string> headers)
        {
            var endpoint = new EndpointDetails();

            if (headers.ContainsKey(Headers.ProcessingEndpoint))
            {
                //todo: remove this line after we have updated to the next unstableversion (due to a bug in the core)
                if (headers[Headers.ProcessingEndpoint] != Configure.EndpointName)
                {
                    endpoint.Name = headers[Headers.ProcessingEndpoint];
                }
            }

            if (headers.ContainsKey(Headers.ProcessingMachine))
            {
                endpoint.Machine = headers[Headers.ProcessingMachine];
            }

            if (!string.IsNullOrEmpty(endpoint.Name) && !string.IsNullOrEmpty(endpoint.Name))
            {
                return endpoint;
            }

            var address = Address.Parse(headers[Headers.OriginatingAddress]);

            //use the failed q to determine the receiving endpoint
            if (headers.ContainsKey("NServiceBus.FailedQ"))
            {
                address = Address.Parse(headers["NServiceBus.FailedQ"]);
            }

            endpoint.FromAddress(address);

            return endpoint;
        }

        void FromAddress(Address address)
        {
            if (string.IsNullOrEmpty(Name))
            {
                Name = address.Queue;
            }

            if (string.IsNullOrEmpty(Machine))
            {
                Name = address.Machine;
            }
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
            FailedInQueue = headers["NServiceBus.FailedQ"];
            TimeOfFailure = DateTimeExtensions.ToUtcDateTime(headers["NServiceBus.TimeOfFailure"]);
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
            return new ExceptionDetails
            {
                ExceptionType = headers["NServiceBus.ExceptionInfo.ExceptionType"],
                Message = headers["NServiceBus.ExceptionInfo.Message"],
                Source = headers["NServiceBus.ExceptionInfo.Source"],
                StackTrace = headers["NServiceBus.ExceptionInfo.StackTrace"]
            };
        }

        public void RegisterException(IDictionary<string,string> headers)
        {
            NumberOfTimesFailed++;

            var timeOfFailure = DateTimeExtensions.ToUtcDateTime(headers["NServiceBus.TimeOfFailure"]);

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