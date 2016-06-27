namespace ServiceControl.MessageRedirects.Handlers
{
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Contracts.MessageRedirects;
    using ServiceControl.MessageRedirects.InternalMessages;

    public class RemoveMessageRedirectHandler : IHandleMessages<RemoveMessageRedirect>
    {
        public IDocumentSession Session { get; set; }

        public IBus Bus { get; set; }

        public void Handle(RemoveMessageRedirect message)
        {
            var messageRedirect = Session.Load<MessageRedirect>(MessageRedirect.GetDocumentIdFromMessageRedirectId(message.MessageRedirectId));

            Session.Delete(messageRedirect);

            Bus.Publish<MessageRedirectRemoved>(evt =>
            {
                evt.MessageRedirectId = message.MessageRedirectId;
                evt.FromPhysicalAddress = messageRedirect.FromPhysicalAddress;
                evt.ToPhysicalAddress = messageRedirect.ToPhysicalAddress;
            });
        }
    }
}