namespace ServiceControl.ExceptionGroups.Archive
{
    using System.Linq;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;

    public class IssueArchiveAllForGroupHandler : IHandleMessages<ArchiveAllForGroup>
    {
        public void Handle(ArchiveAllForGroup message)
        {
            using (var session = Store.OpenSession())
            {
                var group = session.Query<MessageFailureHistory_ByExceptionGroup.ReduceResult, MessageFailureHistory_ByExceptionGroup>()
                    .FirstOrDefault(g => g.ExceptionType == message.GroupId);

                if (@group == null)
                    return;

                var items = session.Load<FailedMessageViewTransformer, FailedMessageView>(@group.FailureHistoryIds);
                foreach (var item in items)
                {
                    Bus.SendLocal(new ArchiveMessage
                    {
                        FailedMessageId = item.Id
                    });
                }
            }
        }

        public IBus Bus { get; set; }
        public IDocumentStore Store { get; set; }
    }
}