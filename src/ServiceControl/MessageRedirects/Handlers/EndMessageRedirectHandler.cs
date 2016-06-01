namespace ServiceControl.MessageRedirects.Handlers
{
    using System;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.MessageRedirects.InternalMessages;

    public class EndMessageRedirectHandler : IHandleMessages<EndMessageRedirect>
    {
        public IDocumentSession Session { get; set; }

        public void Handle(EndMessageRedirect message)
        {
            var messageRedirect = Session.Load<MessageRedirect>(MessageRedirect.GetDocumentIdFromMessageRedirectId(message.MessageRedirectId));

            messageRedirect.ExpiresDateTime = message.ExpiresDateTime;

            messageRedirect.LastModified = DateTime.UtcNow.Ticks;
        }
    }
}