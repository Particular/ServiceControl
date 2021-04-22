namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Raven.Client;

    class FailedMessageArchivedPublisher : EventPublisher<FailedMessageArchived, FailedMessageArchivedPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(FailedMessageArchived @event)
        {
            return new DispatchContext
            {
                FailedMessageId = @event.FailedMessageId
            };
        }

        protected override Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts, IAsyncDocumentSession session)
        {
            return Task.FromResult(contexts.Select(r => (object)new Contracts.FailedMessagesArchived
            {
                FailedMessagesIds = new[] { r.FailedMessageId }
            }));
        }

        public class DispatchContext
        {
            public string FailedMessageId { get; set; }
        }
    }
}