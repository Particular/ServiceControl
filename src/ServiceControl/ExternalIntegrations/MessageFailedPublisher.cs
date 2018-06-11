namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
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

        protected override async Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts, IAsyncDocumentSession session)
        {
            var documentIds = contexts.Select(x => x.FailedMessageId).Cast<ValueType>().ToArray();
            var failedMessageData = await session.LoadAsync<FailedMessage>(documentIds)
                .ConfigureAwait(false);
            
            var failedMessages = new List<object>(failedMessageData.Length);
            foreach (var entity in failedMessageData)
            {
                session.Advanced.Evict(entity);

                if (entity != null)
                {
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