namespace ServiceControl.Groups
{
    using System.Collections.Generic;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Groups.Groupers;
    using ServiceControl.InternalContracts.Messages.MessageFailures;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.InternalMessages;

    public class GroupMessageHandler : IHandleMessages<ImportFailedMessage>
    {
        public IDocumentSession Session { get; set; }

        public IBus Bus { get; set; }

        public IList<IFailedMessageGrouper> Groupers { get; set; }

        public IGroupIdGenerator GroupIdGenerator { get; set; }

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
                var groupName = grouper.GetGroupName(message);

                if (groupName == null)
                {
                    continue;
                }

                var groupType = grouper.GetGroupType(message);
                var groupId = GroupIdGenerator.GenerateId(groupType, groupName);

                var groupExistsOnFailure = failure.FailureGroups.Exists(g => g.Id == groupId);
                if (!groupExistsOnFailure)
                {
                    Bus.SendLocal(new RaiseNewFailureGroupDetectedEvent
                        {
                            GroupId = groupId,
                            GroupName = groupName
                        });

                    failure.FailureGroups.Add(new MessageFailureHistory.FailureGroup
                    {
                        Id = groupId,
                        Title = groupName,
                        Type = groupType
                    });
                }
            }
        }
    }
}