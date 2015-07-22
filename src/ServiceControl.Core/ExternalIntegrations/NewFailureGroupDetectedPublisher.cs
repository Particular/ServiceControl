namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Linq;
    using Raven.Client;
    using ServiceControl.InternalContracts.Messages.MessageFailures;

    public class NewFailureGroupDetectedPublisher : EventPublisher<NewFailureGroupDetected, NewFailureGroupDetectedPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(NewFailureGroupDetected @event)
        {
            return new DispatchContext
            {
                GroupId = @event.GroupId,
                GroupTitle = @event.GroupName
            };
        }

        protected override IEnumerable<object> PublishEvents(IEnumerable<DispatchContext> contexts, IDocumentSession session)
        {
            return contexts.Select(r => new Contracts.NewFailureGroupDetected
            {
                Id = r.GroupId,
                Name = r.GroupTitle
            });
        }

        public class DispatchContext
        {
            public string GroupId { get; set; }
            public string GroupTitle { get; set; }
        }
    }
}
