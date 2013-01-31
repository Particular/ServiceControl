namespace ServiceBus.Management.FailedMessages.Import
{
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

                var failedMessage = session.Load<FailedMessage>(message.IdForCorrelation);

                if (failedMessage == null)
                {
                    failedMessage = new FailedMessage
                    {
                        Id = message.IdForCorrelation,
                        CorrelationId = message.CorrelationId,
                        MessageType = message.Headers[Headers.EnclosedMessageTypes],
                        Headers = message.Headers,
                        Body = DeserializeBody(message),
                        BodyRaw = message.Body,
                        RelatedToMessageId = relatedId,
                        Status = FailedMessageStatus.New
                    };
                }
                else
                {
                    failedMessage.Status = FailedMessageStatus.RepetedFailures;
                }

                failedMessage.NumberOfTimesFailed++;

                session.Store(failedMessage);

                session.SaveChanges();

            }

            return true;
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