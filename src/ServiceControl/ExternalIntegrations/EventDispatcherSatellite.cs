namespace Particular.ServiceControl.ExternalIntegrations
{
    using global::ServiceControl.Contracts.Failures;
    using NServiceBus;
    using NServiceBus.Satellites;

    public class EventDispatcherSatellite : ISatellite
    {
        public const string EntityIdHeaderKey = "ServiceControl.EntityId";
        public const string MessageTypeHeaderKey = "ServiceControl.ExternalEventType";
        public IBus Bus { get; set; }

        public bool Handle(TransportMessage message)
        {
            //var entityId = message.Headers[EntityIdHeaderKey];
            //var messageType = message.Headers[MessageTypeHeaderKey];

            Bus.Publish(new MessageFailed());

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