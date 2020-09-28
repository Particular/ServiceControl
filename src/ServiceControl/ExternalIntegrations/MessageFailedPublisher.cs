namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
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
            await Task.Yield();
            return Enumerable.Empty<object>();
            // TODO: RAVEN5 - Loading multiple documents by id will not work with GUID ids
            //var documentIds = contexts.Select(x => x.FailedMessageId).Cast<ValueType>().ToArray();
            //var failedMessageData = await session.LoadAsync<FailedMessage>(documentIds)
            //    .ConfigureAwait(false);

            //var failedMessages = new List<object>(failedMessageData.Length);
            //foreach (var entity in failedMessageData)
            //{
            //    if (entity != null)
            //    {
            //        session.Advanced.Evict(entity);
            //        failedMessages.Add(entity.ToEvent());
            //    }
            //}

            //return failedMessages;
        }

        public class DispatchContext
        {
            public Guid FailedMessageId { get; set; }
        }
    }
}