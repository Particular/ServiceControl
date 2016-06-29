using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceControl.MessageRedirects.Handlers
{
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Contracts.MessageRedirects;
    using ServiceControl.MessageRedirects.InternalMessages;

    class ChangeMessageRedirectHander : IHandleMessages<ChangeMessageRedirect>
    {
        public IDocumentSession Session { get; set; }

        public IBus Bus { get; set; }

        public void Handle(ChangeMessageRedirect message)
        {
            var messageRedirect = Session.Load<MessageRedirect>(MessageRedirect.GetDocumentIdFromMessageRedirectId(message.MessageRedirectId));

            messageRedirect.ToPhysicalAddress = message.ToPhysicalAddress;

            Session.SaveChanges();

            Bus.Publish<MessageRedirectChanged>(evt =>
            {
                evt.MessageRedirectId = message.MessageRedirectId;
                evt.FromPhysicalAddress = messageRedirect.FromPhysicalAddress;
                evt.ToPhysicalAddress = messageRedirect.ToPhysicalAddress;
            });
        }
    }
}
