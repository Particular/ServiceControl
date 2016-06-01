namespace ServiceControl.MessageRedirects.Handlers
{
    using System;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Contracts.MessageRedirects;
    using ServiceControl.MessageRedirects.InternalMessages;

    public class UpdateMessageRedirectHandler : IHandleMessages<UpdateMessageRedirect>
    {
        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }

        public void Handle(UpdateMessageRedirect message)
        {
            var messageRedirect = Session.Load<MessageRedirect>(MessageRedirect.GetDocumentIdFromMessageRedirectId(message.MessageRedirectId));

            messageRedirect.MatchMessageType = message.MatchMessageType;
            messageRedirect.MatchSourceEndpoint = message.MatchSourceEndpoint;
            messageRedirect.RedirectToEndpoint = messageRedirect.RedirectToEndpoint;
            messageRedirect.AsOfDateTime = message.AsOfDateTime;
            messageRedirect.ExpiresDateTime = message.ExpiresDateTime;

            messageRedirect.LastModified = DateTime.UtcNow.Ticks;

            Bus.Publish(new MessageRedirectUpdated
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