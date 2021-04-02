namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Client;
    using ServiceControl.Recoverability;

    class FailedMessageGroupArchivedPublisher : EventPublisher<FailedMessageGroupArchived, FailedMessageGroupArchivedPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(FailedMessageGroupArchived @event)
        {
            return new DispatchContext
            {
                GroupId = @event.GroupId,
                GroupName = @event.GroupName,
                MessagesCount = @event.MessagesCount,
                FailedMessagesIds = @event.FailedMessagesIds
            };
        }

        protected override Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts, IAsyncDocumentSession session)
        {
            return Task.FromResult(contexts.Select(r => (object)new Contracts.FailedMessageGroupArchived
            {
                GroupId = r.GroupId,
                GroupName = r.GroupName,
                MessagesCount = r.MessagesCount,
                FailedMessagesIds = r.FailedMessagesIds
            }));
        }

        public class DispatchContext
        {
            public string GroupId { get; set; }
            public string GroupName { get; set; }
            public int MessagesCount { get; set; }
            public string[] FailedMessagesIds { get; set; }
        }
    }
}