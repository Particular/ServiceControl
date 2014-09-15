namespace Particular.ServiceControl.ExternalIntegrations
{
    using System.Linq;
    using global::ServiceControl.Contracts.Failures;
    using global::ServiceControl.MessageFailures;
    using NServiceBus;
    using NServiceBus.Satellites;
    using Raven.Client;

    public class EventDispatcherSatellite : ISatellite
    {
        public const string MessageUniqueIdHeaderKey = "ServiceControl.EntityId";
        public IBus Bus { get; set; }
        public IDocumentStore Store { get; set; }

        public bool Handle(TransportMessage message)
        {
            var messageUniqueId = message.Headers[MessageUniqueIdHeaderKey];

            FailedMessage failedMessageData;
            using (var session = Store.OpenSession())
            {
                failedMessageData = session.Load<FailedMessage>(FailedMessage.MakeDocumentId(messageUniqueId));
            }

            if (failedMessageData == null)
            {
                return true;
            }

            var lastProcessingAttempt = failedMessageData.ProcessingAttempts.Last();
            var notification = new MessageFailed
            {
                MessageId = lastProcessingAttempt.MessageId,
                NumberOfProcessingAttempts = failedMessageData.ProcessingAttempts.Count
            };
            Bus.Publish(notification);

            return true;
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public Address InputAddress
        {
            get { return Address.Local.SubScope("ExternalIntegrations"); }
        }
        public bool Disabled
        {
            get { return false; }
        }
    }
}