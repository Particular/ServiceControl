namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class ArchiveAllInGroupHandler : IHandleMessages<ArchiveAllInGroup>
    {
        private bool abort;

        public ArchiveAllInGroupHandler(ShutdownNotifier notifier)
        {
            notifier.Register(() => { abort = true; });
        }

        public void Handle(ArchiveAllInGroup message)
        {
            var query = Session.Query<FailureGroupMessageView, FailedMessages_ByGroup>()
                .Where(m => m.FailureGroupId == message.GroupId && m.Status == FailedMessageStatus.Unresolved)
                .ProjectFromIndexFieldsInto<FailureGroupMessageView>();

            string groupName = null;
            var messageIds = new List<string>();

            using (var stream = Session.Advanced.Stream(query))
            {
                while (!abort && stream.MoveNext())
                {
                    if (stream.Current.Document.Status != FailedMessageStatus.Unresolved)
                    {
                        continue;
                    }

                    if (groupName == null)
                    {
                        groupName = stream.Current.Document.FailureGroupName;
                    }

                    try { 
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

            Bus.Publish<FailedMessageGroupArchived>(m =>
            {
                m.GroupId = message.GroupId;
                m.GroupName = groupName;
                m.MessageIds = messageIds.ToArray();
            });
        }

        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }
    }
}