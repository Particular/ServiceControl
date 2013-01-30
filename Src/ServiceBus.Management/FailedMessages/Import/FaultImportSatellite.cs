namespace ServiceBus.Management.FailedMessages.Import
{
    using System.Text;
    using NServiceBus;
    using NServiceBus.Satellites;
    using Raven.Client;

    public class FaultImportSatellite:ISatellite
    {
        public IDocumentStore Store { get; set; }

        public bool Handle(TransportMessage message)
        {
            using (var session = Store.OpenSession())
            {
                string relatedId = null;

                if (message.Headers.ContainsKey(Headers.RelatedTo))
                    relatedId = message.Headers[Headers.RelatedTo];
                session.Store(new FailedMessage
                    {
                        MessageId = message.Id,
                        IdForCorrelation = message.IdForCorrelation,
                        MessageType = message.Headers[Headers.EnclosedMessageTypes],
                        Headers = message.Headers,
                        BodyText = Encoding.UTF8.GetString(message.Body),//todo - convert to Json of xml/json
                        BodyRaw = message.Body,
                        RelatedToMessageId = relatedId

                    });

                session.SaveChanges();

            }

            return true;
        }

        public void Start()
        {
            
        }

        public void Stop()
        {
            
        }

        public Address InputAddress { get { return Address.Parse("error"); } }
        public bool Disabled {
            get { return false; }
        }
    }
}