namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures;

    public class FailedMessagesImportedPublisher : EventPublisher<FailedMessagesImported, FailedMessagesImportedPublisher.DispatchContext>
    {
        public class DispatchContext
        {
            public Guid[] FailedMessageIds { get; set; }
        }

        protected override DispatchContext CreateDispatchRequest(FailedMessagesImported @event)
        {
            return new DispatchContext
            {
                FailedMessageIds = @event.RepeatedFailureIds.Union(@event.NewFailureIds).Select(x => new Guid(x)).ToArray()
            };
        }

        protected override IEnumerable<object> PublishEvents(IEnumerable<DispatchContext> contexts, IDocumentSession session)
        {
            var documentIds = contexts.SelectMany(x => x.FailedMessageIds).Cast<ValueType>();
            var failedMessageData = session.Load<FailedMessage>(documentIds);
            return failedMessageData.Where(p => p != null).Select(x => x.ToEvent());
        }
    }
}