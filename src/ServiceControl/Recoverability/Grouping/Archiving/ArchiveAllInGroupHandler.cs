namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceControl.MessageFailures;

    public class ArchiveAllInGroupHandler : IHandleMessages<ArchiveAllInGroup>
    {
        public IDocumentSession Session { get; set; }

        public Task Handle(ArchiveAllInGroup message, IMessageHandlerContext context)
        {
            var query = Session.Query<FailureGroupMessageView, FailedMessages_ByGroup>()
                .Where(m => m.FailureGroupId == message.GroupId && m.Status == FailedMessageStatus.Unresolved)
                .ProjectFromIndexFieldsInto<FailureGroupMessageView>();

            string groupName = null;
            var messageIds = new List<string>();

            using (var stream = Session.Advanced.Stream(query))
            {
                while (stream.MoveNext())
                {
                    if (stream.Current.Document.Status != FailedMessageStatus.Unresolved)
                    {
                        continue;
                    }

                    if (groupName == null)
                    {
                        groupName = stream.Current.Document.FailureGroupName;
                    }

                    try
                    {
                        Session.Advanced.DocumentStore.DatabaseCommands.Patch(
                            stream.Current.Document.Id,
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

                        messageIds.Add(stream.Current.Document.MessageId);
                    }
                    catch (ConcurrencyException)
                    {
                        // Ignore concurrency exceptions
                    }
                }
            }

            return context.Publish<FailedMessageGroupArchived>(m =>
            {
                m.GroupId = message.GroupId;
                m.GroupName = groupName;
                m.MessageIds = messageIds.ToArray();
            });
        }
    }
}