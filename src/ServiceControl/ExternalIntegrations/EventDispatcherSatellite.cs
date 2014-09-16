namespace ServiceControl.ExternalIntegrations
{
    using System.Linq;
    using global::ServiceControl.Contracts.Failures;
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

            MessageFailed result;
            using (var session = Store.OpenSession())
            {
                result = session.Query<MessageFailed, ExternalIntegrationsFailedMessagesViewIndex>()
                    .Customize(c => c.WaitForNonStaleResults())
                    .Where(x => x.EntityId == messageUniqueId)
                    .ProjectFromIndexFieldsInto<MessageFailed>()
                    .SingleOrDefault();
            }
            if (result == null)
            {
                return true;
            }
            Bus.Publish(result);
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