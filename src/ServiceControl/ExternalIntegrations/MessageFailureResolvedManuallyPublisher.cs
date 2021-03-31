namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Raven.Client;

    class MessageFailureResolvedManuallyPublisher : EventPublisher<MessageFailureResolvedManually, MessageFailureResolvedManuallyPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(MessageFailureResolvedManually @event)
        {
            return new DispatchContext
            {
                FailedMessageId = new Guid(@event.FailedMessageId)
            };
        }

        protected override Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts, IAsyncDocumentSession session)
        {
            return Task.FromResult(contexts.Select(r => (object)new Contracts.MessageFailureResolvedManually
            {
                FailedMessageId = r.FailedMessageId.ToString()
            }));
        }

        public class DispatchContext
        {
            public Guid FailedMessageId { get; set; }
        }
    }
}