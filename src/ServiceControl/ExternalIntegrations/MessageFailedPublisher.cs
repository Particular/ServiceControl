namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using MessageFailures;
    using Raven.Client.Documents.Session;

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
            // TODO: RAVEN5 - Loading multiple documents by id will not work with GUID ids
            var documentIds = contexts.Select(x => $"FailedMessages/{x.FailedMessageId}").ToArray();
            var failedMessageData = await session.LoadAsync<FailedMessage>(documentIds)
                .ConfigureAwait(false);

            var failedMessages = new List<object>(failedMessageData.Values.Count);
            foreach (var entity in failedMessageData.Values)
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