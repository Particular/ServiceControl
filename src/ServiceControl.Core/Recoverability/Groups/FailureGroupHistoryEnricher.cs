namespace ServiceControl.Recoverability.Groups
{
    using System.Collections.Generic;
    using Particular.Operations.Ingestion.Api;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageFailures;
    using ServiceControl.Recoverability.Groups.Groupers;

    public class FailureGroupHistoryEnricher : IEnrichMessageFailureHistory
    {
        public void Enrich(MessageFailureHistory history, IngestedMessage actualMessage, FailureDetails failureDetails)
        {
            foreach (var grouper in Groupers)
            {
                var groupName = grouper.GetGroupName(actualMessage, failureDetails);

                if (groupName == null)
                {
                    continue;
                }

                var groupType = grouper.GroupType;
                var groupId = GroupIdGenerator.GenerateId(groupType, groupName);
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

        public IList<IFailedMessageGrouper> Groupers { get; set; }
        public IGroupIdGenerator GroupIdGenerator { get; set; }
    }
}