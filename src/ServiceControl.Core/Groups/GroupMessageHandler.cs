namespace ServiceControl.Groups
{
    using System.Collections.Generic;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Groups.Groupers;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.InternalMessages;

    public class GroupMessageHandler : IHandleMessages<ImportFailedMessage>
    {
        public IDocumentSession Session { get; set; }

        public IBus Bus { get; set; }

        public IList<IFailedMessageGrouper> Groupers { get; set; }

        public void Handle(ImportFailedMessage message)
        {
            if (Groupers == null)
            {
                return;
            }

            var failure = Session.Load<MessageFailureHistory>(MessageFailureHistory.MakeDocumentId(message.UniqueMessageId));
            if (failure == null)
            {
                return;
            }

            foreach (var grouper in Groupers)
            {
                var groupId = grouper.GetGroupId(message);
                if (groupId == null)
                {
                    continue;
                }
                
                var groupExistsOnFailure = failure.FailureGroups.Exists(g => g.Id == groupId);
                if (!groupExistsOnFailure)
                {
                    failure.FailureGroups.Add(new MessageFailureHistory.FailureGroup
                    {
                        Id = groupId,
                        Title = grouper.GetGroupName(message)
                    });
                }
            }
        }
    }
}