namespace ServiceControl.Recoverability.Groups
{
    using System.Collections.Generic;
    using ServiceControl.MessageFailures;
    using ServiceControl.Recoverability.Groups.Groupers;

    public class MessageFailureHistoryGrouper
    {
        IList<IFailedMessageGrouper> groupers;
        IGroupIdGenerator groupIdGenerator;

        public MessageFailureHistoryGrouper(IList<IFailedMessageGrouper> groupers, IGroupIdGenerator groupIdGenerator)
        {
            this.groupers = groupers;
            this.groupIdGenerator = groupIdGenerator;
        }

        public void Group(MessageFailureHistory history)
        {
            foreach (var grouper in groupers)
            {
                var groupName = grouper.GetGroupName(history);
                var groupType = grouper.GroupType;
                var groupId = groupIdGenerator.GenerateId(groupType, groupName);
                var groupExistsOnFailure = history.FailureGroups.Exists(g => g.Id == groupId);
                if (!groupExistsOnFailure)
                {
                    history.FailureGroups.Add(new MessageFailureHistory.FailureGroup
                    {
                        Id = groupId, 
                        Title = groupName, 
                        Type = groupType
                    });
                }
            }
        }

        public int NumberOfAvailableGroupers()
        {
            return groupers.Count;
        }
    }
}