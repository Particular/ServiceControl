namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.MessageFailures;
    using Raven.Client;

    public class FailedMessageArchivedPublisher : EventPublisher<FailedMessageArchived, MessageFailedPublisher.DispatchContext>
    {
        protected override MessageFailedPublisher.DispatchContext CreateDispatchRequest(FailedMessageArchived @event)
        {
            return new MessageFailedPublisher.DispatchContext
            {
                FailedMessageId = new Guid(@event.FailedMessageId)
            };
        }

        protected override IEnumerable<object> PublishEvents(IEnumerable<MessageFailedPublisher.DispatchContext> contexts, IDocumentSession session)
        {
            // FailedMessageArchived events are published externally as ServiceControl.Contracts.MessageFailed events
            // with the Status property set to MessageStatus.ArchivedFailure. This is handled by the PublishEvents
            // method in the MessageFailedPublisher class. Returning an empty collection here to avoid multiple 
            // external events from being published.

            return Enumerable.Empty<object>();
        }
    }
}
