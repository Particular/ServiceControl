namespace ServiceControl.MessageRedirects.Handlers
{
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.MessageRedirects.InternalMessages;
    using ServiceControl.Contracts.MessageRedirects;

    public class CreateMessageRedirectHandler : IHandleMessages<CreateMessageRedirect>
    {
        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }

        public void Handle(CreateMessageRedirect message)
        {
            var messageRedirect = new MessageRedirect
            {
                Id = MessageRedirect.GetDocumentIdFromMessageRedirectId(message.MessageRedirectId),
                FromPhysicalAddress = message.FromPhysicalAddress,
                ToPhysicalAddress = message.ToPhysicalAddress
            };

            Session.Store(messageRedirect);

            Bus.Publish(new MessageRedirectCreated
            {
                MessageRedirectId = messageRedirect.Id,
                FromPhysicalAddress = messageRedirect.FromPhysicalAddress,
                ToPhysicalAddress = messageRedirect.ToPhysicalAddress
            });
        }
    }
}
