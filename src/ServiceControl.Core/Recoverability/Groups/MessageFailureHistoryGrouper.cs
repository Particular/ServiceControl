namespace ServiceControl.Recoverability.Groups
{
    using System.Collections.Generic;
    using System.Linq;
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
            if (groupers == null)
                return;

            history.FailureGroups = new List<MessageFailureHistory.FailureGroup>();

            foreach (var grouper in groupers)
            {
                var groupName = grouper.GetGroupName(history);
                var groupType = grouper.GroupType;
                var groupId = groupIdGenerator.GenerateId(groupType, groupName);
                
                history.FailureGroups.Add(new MessageFailureHistory.FailureGroup
                {
                    Id = groupId, 
                    Title = groupName, 
                    Type = groupType
                });
            }
        }

        public string GetGrouperSetId()
        {
            var groupersOrderedByType = groupers.OrderBy(f => f.GroupType).Select(i => i.GroupType);
            return string.Join(";", groupersOrderedByType);
        }
    }
}