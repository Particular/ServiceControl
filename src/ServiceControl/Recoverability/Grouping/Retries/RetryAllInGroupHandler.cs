namespace ServiceControl.Recoverability
{
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using Raven.Client;

    public class RetryAllInGroupHandler : IHandleMessages<RetryAllInGroup>
    {
        public Task Handle(RetryAllInGroup message, IMessageHandlerContext context)
        {
            if (Retries == null)
            {
                return Task.FromResult(0);
            }

            var group = Session.Query<FailureGroupView, FailureGroupsViewIndex>()
                               .FirstOrDefault(x => x.Id == message.GroupId);

            string title = null;

            if (group != null && group.Title != null)
            {
                title = group.Title;
            }

            Retries.StartRetryForIndex<FailureGroupMessageView, FailedMessages_ByGroup>(x => x.FailureGroupId == message.GroupId, title);

            return Task.FromResult(0);
        }

        public RetriesGateway Retries { get; set; }
        public IDocumentSession Session { get; set; }
    }
}