namespace Particular.ServiceControl.ExternalIntegrations
{
    using global::ServiceControl.Contracts.MessageFailures;
    using NServiceBus;
    using NServiceBus.Transports;

    public class EventMappingHandler : IHandleMessages<IEvent>
    {
        public ISendMessages MessageSender { get; set; }

        public void Handle(IEvent message)
        {
            var failedMessageEvent = message as MessageFailed;
            if (failedMessageEvent == null)
            {
                return;
            }
            var pushRequest = new TransportMessage();
            pushRequest.Headers[EventDispatcherSatellite.MessageUniqueIdHeaderKey] = failedMessageEvent.FailedMessageId;
            MessageSender.Send(pushRequest, Address.Local.SubScope("ExternalIntegrations"));
        }
    }
}