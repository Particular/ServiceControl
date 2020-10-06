namespace ServiceControl.Recoverability
{
    using System.Linq;
    using MessageFailures;
    using Raven.Client.Documents.Indexes;

    public class ArchivedGroupsViewIndex : AbstractIndexCreationTask<FailedMessage, FailureGroupView>
    {
        public ArchivedGroupsViewIndex()
        {
            Map = docs => from doc in docs
                //TODO: RAVEN5 type mismatch when SaveEnumsAsInt
                where doc.Status.ToString() == "Archived"
                let failureTimes = doc.ProcessingAttempts.Select(x => x.FailureDetails.TimeOfFailure)
                from failureGroup in doc.FailureGroups
                select new FailureGroupView
                {
                    Id = failureGroup.Id,
                    Title = failureGroup.Title,
                    Count = 1,
                    First = failureTimes.Min(),
                    Last = failureTimes.Max(),
                    Type = failureGroup.Type
                };

            Reduce = results => from result in results
                group result by new
                {
                    result.Id,
                    result.Title,
                    result.Type
                }
                into g
                select new FailureGroupView
                {
                    Id = g.Key.Id,
                    Title = g.Key.Title,
                    Count = g.Sum(x => x.Count),
                    First = g.Min(x => x.First),
                    Last = g.Max(x => x.Last),
                    Type = g.Key.Type
                };
        }
    }
}