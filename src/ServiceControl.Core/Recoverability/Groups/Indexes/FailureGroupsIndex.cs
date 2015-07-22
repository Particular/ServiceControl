namespace ServiceControl.Recoverability.Groups.Indexes
{
    using System.Linq;
    using Raven.Client.Indexes;
    using ServiceControl.MessageFailures;

    public class FailureGroupsIndex : AbstractIndexCreationTask<MessageFailureHistory, FailureGroup>
    {
        public FailureGroupsIndex()
        {
            Map = failures => from failure in failures
                              where failure.Status == FailedMessageStatus.Unresolved
                let latestAttempt = failure.ProcessingAttempts.Last()
                let firstAttempt = failure.ProcessingAttempts.First()
                from failureGroup in failure.FailureGroups
                select new FailureGroup
                {
                    Id = failureGroup.Id,
                    Title = failureGroup.Title,
                    Count = 1,
                    First = firstAttempt.FailureDetails.TimeOfFailure,
                    Last = latestAttempt.FailureDetails.TimeOfFailure,
                    Type = failureGroup.Type
                };

            Reduce = results => from result in results
                group result by result.Id into g
                select new FailureGroup
                {
                    Id = g.Key, 
                    Title = g.First().Title, 
                    Count = g.Sum(x => x.Count), 
                    First = g.Min(x => x.First), 
                    Last = g.Max(x => x.Last),
                    Type = g.First().Type, 
                };
        }
    }
}