namespace ServiceBus.Management.FailedMessages
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using NServiceBus;
    using NServiceBus.Satellites;
    using Newtonsoft.Json;
    using Raven.Client;

    public class FaultImportSatellite : ISatellite
    {
        public IDocumentStore Store { get; set; }

        public bool Handle(TransportMessage message)
        {
            using (var session = Store.OpenSession())
            {
                string relatedId = null;

                if (message.Headers.ContainsKey(Headers.RelatedTo))
                    relatedId = message.Headers[Headers.RelatedTo];

                var failedMessage = session.Load<Message>(message.IdForCorrelation);

                if (failedMessage == null)
                {
                    failedMessage = new Message
                        {
                            Id = message.IdForCorrelation,
                            CorrelationId = message.CorrelationId,
                            MessageType = message.Headers[Headers.EnclosedMessageTypes],
                            Headers = message.Headers.Select(header => new KeyValuePair<string, string>(header.Key, header.Value)),
                            TimeSent = DateTimeExtensions.ToUtcDateTime(message.Headers[Headers.TimeSent]),
                            Body = DeserializeBody(message),
                            BodyRaw = message.Body,
                            RelatedToMessageId = relatedId,
                            ConversationId = message.Headers[Headers.ConversationId],
                            Status = MessageStatus.Failed,
                            Endpoint = GetEndpoint(message),
                            FailureDetails = new FailureDetails
                                {
                                    FailedInQueue = message.Headers["NServiceBus.FailedQ"],
                                    TimeOfFailure =
                                        DateTimeExtensions.ToUtcDateTime(message.Headers["NServiceBus.TimeOfFailure"])
                                }
                        };
                }
                else
                {
                    failedMessage.Status = MessageStatus.RepeatedFailures;
                }

                failedMessage.FailureDetails.Exception = GetException(message);
                failedMessage.FailureDetails.NumberOfTimesFailed++;

                session.Store(failedMessage);

                session.SaveChanges();

            }

            return true;
        }

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

        string GetEndpoint(TransportMessage message)
        {
            if (message.Headers.ContainsKey(Headers.OriginatingEndpoint))
                return message.Headers[Headers.OriginatingEndpoint];

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

        public void Start()
        {

        }

        public void Stop()
        {

        }

        public Address InputAddress { get { return Address.Parse("error"); } }

        public bool Disabled { get { return false; } }
    }
}