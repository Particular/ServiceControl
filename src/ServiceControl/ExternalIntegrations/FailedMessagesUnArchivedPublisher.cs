namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Raven.Client;

    class FailedMessagesUnArchivedPublisher : EventPublisher<FailedMessagesUnArchived, FailedMessagesUnArchivedPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(FailedMessagesUnArchived @event)
        {
            return new DispatchContext
            {
                MessageIds = @event.DocumentIds,
                MessagesCount = @event.MessagesCount
            };
        }

        protected override Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts, IAsyncDocumentSession session)
        {
            return Task.FromResult(contexts.Select(r => (object)new Contracts.FailedMessagesUnArchived
            {
                FailedMessagesIds = r.MessageIds
            }));
        }

        public class DispatchContext
        {
            public string[] MessageIds { get; set; }
            public int MessagesCount { get; set; }
        }
    }
}