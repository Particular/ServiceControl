namespace ServiceControl.MessageRedirects.Handlers
{
    using System;
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
                MatchMessageType = message.MatchMessageType,
                MatchSourceEndpoint = message.MatchSourceEndpoint,
                RedirectToEndpoint = message.RedirectToEndpoint,
                AsOfDateTime = message.AsOfDateTime,
                ExpiresDateTime = message.ExpiresDateTime,
                LastModified = DateTime.UtcNow.Ticks
            };

            Session.Store(messageRedirect);

            Bus.Publish(new MessageRedirectCreated
            {
                MessageRedirectId = messageRedirect.Id,
                MatchMessageType = messageRedirect.MatchMessageType,
                MatchSourceEndpoint = messageRedirect.MatchSourceEndpoint,
                RedirectToEndpoint = messageRedirect.RedirectToEndpoint,
                AsOfDateTime = messageRedirect.AsOfDateTime,
                ExpiresDateTime = messageRedirect.ExpiresDateTime
            });
        }
    }
}
