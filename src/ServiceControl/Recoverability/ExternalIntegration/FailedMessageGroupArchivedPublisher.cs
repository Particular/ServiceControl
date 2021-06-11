namespace ServiceControl.Recoverability.ExternalIntegration
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using ExternalIntegrations;
    using Raven.Client;
    using Recoverability;

    class FailedMessageGroupBatchArchivedPublisher : EventPublisher<FailedMessageGroupBatchArchived, FailedMessageGroupBatchArchivedPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(FailedMessageGroupBatchArchived @event)
        {
            return new DispatchContext
            {
                FailedMessagesIds = @event.FailedMessagesIds
            };
        }

        protected override Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts, IAsyncDocumentSession session)
        {
            return Task.FromResult(contexts.Select(r => (object)new Contracts.FailedMessagesArchived
            {
                FailedMessagesIds = r.FailedMessagesIds
            }));
        }

        public class DispatchContext
        {
            public string[] FailedMessagesIds { get; set; }
        }
    }
}