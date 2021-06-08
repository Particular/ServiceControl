namespace ServiceControl.Recoverability.ExternalIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using ExternalIntegrations;
    using MessageFailures;
    using Raven.Client;

    class MessageFailedPublisher : EventPublisher<MessageFailed, MessageFailedPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(MessageFailed @event)
        {
            return new DispatchContext
            {
                FailedMessageId = new Guid(@event.FailedMessageId)
            };
        }

        protected override async Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts, IAsyncDocumentSession session)
        {
            var documentIds = contexts.Select(x => x.FailedMessageId).Cast<ValueType>().ToArray();
            var failedMessageData = await session.LoadAsync<FailedMessage>(documentIds)
                .ConfigureAwait(false);

            var failedMessages = new List<object>(failedMessageData.Length);
            foreach (var entity in failedMessageData)
            {
                if (entity != null)
                {
                    session.Advanced.Evict(entity);
                    failedMessages.Add(entity.ToEvent());
                }
            }

            return failedMessages;
        }

        public class DispatchContext
        {
            public Guid FailedMessageId { get; set; }
        }
    }
}