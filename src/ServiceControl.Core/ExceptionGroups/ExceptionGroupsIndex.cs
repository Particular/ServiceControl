namespace ServiceControl.ExceptionGroups
{
    using System;
    using System.Linq;
    using Raven.Client.Indexes;
    using ServiceControl.MessageFailures;

    public class ExceptionGroupsIndex : AbstractIndexCreationTask<MessageFailureHistory, ExceptionGroup>
    {
        public ExceptionGroupsIndex()
        {
            Map = docs => from doc in docs
                let latestAttempt = doc.ProcessingAttempts.Last()
                select new ExceptionGroup
                {
                    Id = latestAttempt.FailureDetails.Exception.ExceptionType, 
                    Title = String.Empty,
                    Count = 1, 
                    First = latestAttempt.FailureDetails.TimeOfFailure, 
                    Last = latestAttempt.FailureDetails.TimeOfFailure, 
                };

            Reduce = results => from result in results
                group result by result.Id into g
                select new ExceptionGroup
                {
                    Id = g.Key, 
                    Title = g.Key, 
                    Count = g.Sum(x => x.Count), 
                    First = g.Min(x => x.First), 
                    Last = g.Max(x => x.Last)
                };

            
        }
    }
}