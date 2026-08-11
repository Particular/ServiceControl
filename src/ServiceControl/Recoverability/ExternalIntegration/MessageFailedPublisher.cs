namespace ServiceControl.Recoverability.ExternalIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using ExternalIntegrations;
    using ServiceControl.Persistence;

    class MessageFailedPublisher : EventPublisher<MessageFailed, MessageFailedPublisher.DispatchContext>
    {
        readonly IFailedMessageQueryDataStore dataStore;

        public MessageFailedPublisher(IFailedMessageQueryDataStore dataStore)
        {
            this.dataStore = dataStore;
        }

        protected override DispatchContext CreateDispatchRequest(MessageFailed @event)
        {
            return new DispatchContext
            {
                FailedMessageId = new Guid(@event.FailedMessageId)
            };
        }

        protected override async Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts, CancellationToken cancellationToken = default)
        {
            var ids = contexts.Select(x => x.FailedMessageId).ToArray();
            var results = await dataStore.GetFailedMessagesByIds(ids);
            return results.Select(x => x.ToEvent());
        }

        public class DispatchContext
        {
            public Guid FailedMessageId { get; set; }
        }
    }
}