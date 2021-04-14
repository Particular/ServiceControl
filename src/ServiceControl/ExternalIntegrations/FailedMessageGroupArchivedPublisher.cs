namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Client;
    using ServiceControl.Recoverability;

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
                // cleanup FailedMessages/ publish guids without document collection name
                FailedMessagesIds = r.FailedMessagesIds.Select(id => id.Replace("FailedMessages/", "")).ToArray()
            }));
        }

        public class DispatchContext
        {
            public string[] FailedMessagesIds { get; set; }
        }
    }
}