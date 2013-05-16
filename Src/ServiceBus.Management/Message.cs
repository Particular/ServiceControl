namespace ServiceBus.Management
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Unicast.Transport;
    using Newtonsoft.Json;

    public class Message
    {
        public Message()
        {
        }

        public Message(TransportMessage message)
        {
            ReceivingEndpoint = EndpointDetails.ReceivingEndpoint(message);
            Id = message.IdForCorrelation + "-" + ReceivingEndpoint.Name;
            MessageId = message.IdForCorrelation;
            CorrelationId = message.CorrelationId;
            Recoverable = message.Recoverable;
            MessageIntent = message.MessageIntent;
            Headers = message.Headers.Select(header => new KeyValuePair<string, string>(header.Key, header.Value));
            TimeSent = DateTimeExtensions.ToUtcDateTime(message.Headers[NServiceBus.Headers.TimeSent]);

            if (message.IsControlMessage())
            {
                MessageType = "SystemMessage";
            }
            else
            {
                MessageType = message.Headers[NServiceBus.Headers.EnclosedMessageTypes];
                ContentType = DetermineContentType(message);
                Body = DeserializeBody(message, ContentType);
                BodyRaw = message.Body;
                RelatedToMessageId = message.Headers.ContainsKey(NServiceBus.Headers.RelatedTo) ? message.Headers[NServiceBus.Headers.RelatedTo] : null;
                ConversationId = message.Headers[NServiceBus.Headers.ConversationId];
                OriginatingSaga = SagaDetails.Parse(message);
                IsDeferredMessage = message.Headers.ContainsKey(NServiceBus.Headers.IsDeferredMessage);
            }

            OriginatingEndpoint = EndpointDetails.OriginatingEndpoint(message);
        }


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

        ICollection<HistoryItem> history { get; set; }


        string DetermineContentType(TransportMessage message)
        {
            if (message.Headers.ContainsKey(NServiceBus.Headers.ContentType))
            {
                return message.Headers[NServiceBus.Headers.ContentType];
            }

            return "text/xml"; //default to xml for now
        }

        static string DeserializeBody(TransportMessage message, string contentType)
        {
            var bodyString = Encoding.UTF8.GetString(message.Body);

            if (contentType == "text/json")
            {
                return bodyString;
            }

            if (contentType != "text/xml")
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
            var rawMessage = new TransportMessage
            {
                Body = BodyRaw,
                CorrelationId = CorrelationId,
                Recoverable = Recoverable,
                MessageIntent = MessageIntent,
                ReplyToAddress = Address.Parse(ReplyToAddress),
                Headers = Headers.Where(kv => !KeysToRemoveWhenRetryingAMessage.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value)
            };


            Status = MessageStatus.RetryIssued;

            History.Add(new HistoryItem
                {
                    Action = "RetryIssued",
                    Time = requestedAt
                });

            return rawMessage;
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

        public void Update(TransportMessage message)
        {
            var processedAt = DateTimeExtensions.ToUtcDateTime(message.Headers[NServiceBus.Headers.ProcessingEnded]);

            if (Status == MessageStatus.Successful && ProcessedAt > processedAt)
            {
                return; //don't overwrite since this message is older
            }

            if (BodyRaw.Length != message.Body.Length)
                throw new InvalidOperationException("Message bodies differ, message has been tampered with");


            ProcessedAt = processedAt;

            if (Status != MessageStatus.Successful)
            {
                FailureDetails.ResolvedAt = DateTimeExtensions.ToUtcDateTime(message.Headers[NServiceBus.Headers.ProcessingEnded]);
                History.Add(new HistoryItem
                    {
                        Action = "ErrorResolved",
                        Time = FailureDetails.ResolvedAt
                    });
            }

            Status = MessageStatus.Successful;

            if (message.Headers.ContainsKey("NServiceBus.OriginatingAddress"))
            {
                ReplyToAddress = message.Headers["NServiceBus.OriginatingAddress"];
            }

            Statistics = GetProcessingStatistics(message);

        }

        public void MarkAsSuccessful(TransportMessage message)
        {
            Status = MessageStatus.Successful;
            ProcessedAt = DateTimeExtensions.ToUtcDateTime(message.Headers[NServiceBus.Headers.ProcessingEnded]);
            Statistics = GetProcessingStatistics(message);

            if (message.Headers.ContainsKey("NServiceBus.OriginatingAddress"))
            {
                ReplyToAddress = message.Headers["NServiceBus.OriginatingAddress"];
            }

        }

        MessageStatistics GetProcessingStatistics(TransportMessage message)
        {
            return new MessageStatistics
            {
                CriticalTime =
                    DateTimeExtensions.ToUtcDateTime(message.Headers[NServiceBus.Headers.ProcessingEnded]) -
                    DateTimeExtensions.ToUtcDateTime(message.Headers[NServiceBus.Headers.TimeSent]),
                ProcessingTime =
                    DateTimeExtensions.ToUtcDateTime(message.Headers[NServiceBus.Headers.ProcessingEnded]) -
                    DateTimeExtensions.ToUtcDateTime(message.Headers[NServiceBus.Headers.ProcessingStarted])
            };
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

            if (message.Headers.ContainsKey(Headers.ProcessingEndpoint))
            {
                //todo: remove this line after we have updated to the next unstableversion (due to a bug in the core)
                if (message.Headers[Headers.ProcessingEndpoint] != Configure.EndpointName)
                    endpoint.Name = message.Headers[Headers.ProcessingEndpoint];
            }

            if (message.Headers.ContainsKey(Headers.ProcessingMachine))
            {
                endpoint.Machine = message.Headers[Headers.ProcessingMachine];
            }

            if (!string.IsNullOrEmpty(endpoint.Name) && !string.IsNullOrEmpty(endpoint.Name))
            {
                return endpoint;
            }

            var address = message.ReplyToAddress;

            //use the failed q to determine the receiving endpoint
            if (message.Headers.ContainsKey("NServiceBus.FailedQ"))
            {
                address = Address.Parse(message.Headers["NServiceBus.FailedQ"]);
            }

            endpoint.FromAddress(address);

            return endpoint;
        }

        void FromAddress(Address address)
        {
            if (string.IsNullOrEmpty(Name))
                Name = address.Queue;

            if (string.IsNullOrEmpty(Machine))
                Name = address.Machine;
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
        public MessageStatistics()
        {
            
        }

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
        public ExceptionDetails()
        {
            
        }

        public string ExceptionType { get; set; }

        public string Message { get; set; }

        public string Source { get; set; }

        public string StackTrace { get; set; }
    }
}