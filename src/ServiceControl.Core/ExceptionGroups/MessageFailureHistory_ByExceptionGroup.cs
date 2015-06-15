namespace ServiceControl.ExceptionGroups
{
    using System;
    using System.Linq;
    using Raven.Client.Indexes;
    using ServiceControl.MessageFailures;

    public class MessageFailureHistory_ByExceptionGroup : AbstractIndexCreationTask<MessageFailureHistory, MessageFailureHistory_ByExceptionGroup.ReduceResult>
    {
        public class ReduceResult
        {
            public string ExceptionType { get; set; }
            public int Count { get; set; }
            public DateTime First { get; set; }
            public DateTime Last { get; set; }
            // TODO: Do we need to add FailureTime and MessageType to this for sorting purposes?
            public string[] FailureHistoryIds { get; set; }
        }

        public MessageFailureHistory_ByExceptionGroup()
        {
            Map = docs => from doc in docs
                          where doc.Status == FailedMessageStatus.Unresolved
                let failure = doc.ProcessingAttempts.Last().FailureDetails
                select new
                {
                    failure.Exception.ExceptionType,
                    Count = 1,
                    First = failure.TimeOfFailure,
                    Last = failure.TimeOfFailure,
                    FailureHistoryIds = new[] { doc.Id }
                };

            Reduce = results => from result in results
                group result by result.ExceptionType
                into g
                select new
                {
                    ExceptionType = g.Key,
                    Count = g.Sum(x => x.Count),
                    First = g.Min(x => x.First),
                    Last = g.Max(x => x.Last),
                    FailureHistoryIds = g.SelectMany(x => x.FailureHistoryIds)
                };
        }
    }
}