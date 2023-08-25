namespace ServiceControl.Recoverability.ExternalIntegration
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using ExternalIntegrations;
    using Recoverability;

    class FailedMessageGroupBatchUnarchivedPublisher : EventPublisher<FailedMessageGroupBatchUnarchived, FailedMessageGroupBatchUnarchivedPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(FailedMessageGroupBatchUnarchived @event)
        {
            return new DispatchContext
            {
                FailedMessagesIds = @event.FailedMessagesIds
            };
        }

        protected override Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts)
        {
            return Task.FromResult(contexts.Select(r => (object)new Contracts.FailedMessagesUnArchived()
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