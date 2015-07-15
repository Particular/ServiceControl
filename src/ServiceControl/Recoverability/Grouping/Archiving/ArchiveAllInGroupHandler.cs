namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;

    public class ArchiveAllInGroupHandler : IHandleMessages<ArchiveAllInGroup>
    {
        public void Handle(ArchiveAllInGroup message)
        {
            var query = Session.Query<FailureGroupMessageView, FailedMessages_ByGroup>()
                .Where(m => m.FailureGroupId == message.GroupId && m.Status == FailedMessageStatus.Unresolved)
                .TransformWith<FailedMessageViewTransformer, FailedMessageView>();

            using (var stream = Session.Advanced.Stream(query))
            {
                while (stream.MoveNext())
                {
                    Session.Advanced.DocumentStore.DatabaseCommands.Patch(
                        FailedMessage.MakeDocumentId(stream.Current.Document.MessageId),
                        new[]
                        {
                            new PatchRequest
                            {
                                Type = PatchCommandType.Set,
                                Name = "Status",
                                Value = (int) FailedMessageStatus.Archived,
                                PrevVal = (int) FailedMessageStatus.Unresolved
                            }
                        });
                }
            }

            Bus.Publish<FailedMessageGroupArchived>(m => m.GroupId = message.GroupId);
        }

        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }
    }
}