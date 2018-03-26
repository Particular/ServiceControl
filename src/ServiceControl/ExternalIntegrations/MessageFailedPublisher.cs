namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures;

    public class MessageFailedPublisher : EventPublisher<MessageFailed, MessageFailedPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(MessageFailed @event)
        {
            return new DispatchContext
            {
                FailedMessageId = new Guid(@event.FailedMessageId)
            };
        }

        protected override IEnumerable<object> PublishEvents(IEnumerable<DispatchContext> contexts, IDocumentSession session)
        {
            var documentIds = contexts.Select(x => x.FailedMessageId).Cast<ValueType>().ToArray();
            var failedMessageData = session.Load<FailedMessage>(documentIds);
            foreach (var entity in failedMessageData)
            {
                session.Advanced.Evict(entity);
            }

            return failedMessageData.Where(p => p != null).Select(x => x.ToEvent());
        }

        public class DispatchContext
        {
            public Guid FailedMessageId { get; set; }
        }


    }
}